using System.Text.RegularExpressions;
using Ajure.Specification;

namespace Ajure.Validation;

public sealed record DeterministicInput
{
    public required ProjectSpec Spec { get; init; }

    public required DocumentContext Context { get; init; }

    public IReadOnlyList<RenderedDocument> Documents { get; init; } = [];

    public IReadOnlyList<RenderedTargetFile> TargetFiles { get; init; } = [];
}

public sealed record DeterministicResult
{
    public IReadOnlyList<Finding> Findings { get; init; } = [];

    public required bool AcceptanceCoverageComplete { get; init; }

    public required bool AcceptanceCriteriaVerifiable { get; init; }

    public required bool SecurityDecisionsPresent { get; init; }

    public required bool SecretsOrCodeDetected { get; init; }

    public required bool TargetFilesValid { get; init; }

    public required bool ArtifactVersionsConsistent { get; init; }

    public required bool HasUnresolvedCriticalDecisions { get; init; }

    public IReadOnlyList<string> MustRequirementsMissingFromTargets { get; init; } = [];

    public bool Passed => Findings.All(static finding => finding.Severity != FindingSeverity.Critical);
}

/// <summary>Deterministic validation stage (TRD 8.1, EVALUATION Stage 2). No model call happens here.</summary>
public static partial class DeterministicValidator
{
    private const string MissingContentMarker = "_Not provided in the current specification._";

    private static readonly string[] PlaceholderPatterns =
    [
        "TBD",
        "TODO",
        "to be determined",
        "나중에 결정",
        "적절한 방식"
    ];

    private static readonly string[] AmbiguousWords =
    [
        "빠르게",
        "직관적으로",
        "안전하게",
        "예쁘게",
        "fast",
        "intuitive",
        "secure enough",
        "user friendly"
    ];

    private static readonly (string Path, int Minimum, int Maximum)[] LengthBudgets =
    [
        (DocumentRenderer.IdeationPath, 100, 220),
        (DocumentRenderer.PrdPath, 180, 450),
        (DocumentRenderer.TrdPath, 220, 550)
    ];

    public static DeterministicResult Validate(DeterministicInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        var spec = input.Spec;
        var graph = RequirementGraph.Build(spec);
        var findings = new List<Finding>();

        CheckIdentifiers(spec, findings);
        var missingAcceptance = graph.RequirementsWithoutAcceptance();
        foreach (var id in missingAcceptance)
        {
            findings.Add(Create(
                $"det-missing-ac-{id}",
                FindingSeverity.Critical,
                RuleKeys.MissingAcceptanceCriterion,
                $"Requirement {id} has no linked acceptance criterion.",
                [id]));
        }

        foreach (var id in graph.RequirementsWithoutTechnicalDecision())
        {
            findings.Add(Create(
                $"det-missing-td-{id}",
                FindingSeverity.Major,
                RuleKeys.TraceabilityBreak,
                $"Requirement {id} has neither a technical decision nor a 'no technical impact' marker.",
                [id]));
        }

        var unverifiable = CheckAcceptanceCriteria(spec, findings);
        CheckMeasurements(spec, findings);
        CheckNonGoals(spec, findings);
        CheckStates(spec, findings);
        var securityPresent = CheckTechnicalProfile(spec, findings);
        var unresolvedCritical = CheckOpenDecisions(spec, findings);
        var secretsOrCode = CheckArtifactContent(input, findings);
        var (targetFilesValid, missingMustIds) = CheckTargetFiles(input, findings);
        var versionsConsistent = CheckVersions(input, findings);
        CheckLengthBudgets(input, findings);

        return new DeterministicResult
        {
            Findings = [.. findings.OrderByDescending(static finding => finding.Severity).ThenBy(static finding => finding.Id, StringComparer.Ordinal)],
            AcceptanceCoverageComplete = missingAcceptance.Count == 0,
            AcceptanceCriteriaVerifiable = !unverifiable,
            SecurityDecisionsPresent = securityPresent,
            SecretsOrCodeDetected = secretsOrCode,
            TargetFilesValid = targetFilesValid,
            ArtifactVersionsConsistent = versionsConsistent,
            HasUnresolvedCriticalDecisions = unresolvedCritical,
            MustRequirementsMissingFromTargets = missingMustIds
        };
    }

    private static void CheckIdentifiers(ProjectSpec spec, List<Finding> findings)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var id in spec.AllIds())
        {
            if (!SpecIds.IsValid(id))
            {
                findings.Add(Create(
                    $"det-id-format-{id}",
                    FindingSeverity.Critical,
                    RuleKeys.TraceabilityBreak,
                    $"Identifier '{id}' does not follow the stable id format.",
                    []));
            }

            if (!seen.Add(id))
            {
                findings.Add(Create(
                    $"det-id-duplicate-{id}",
                    FindingSeverity.Critical,
                    RuleKeys.TraceabilityBreak,
                    $"Identifier '{id}' is used more than once.",
                    [id]));
            }
        }

        var criteriaIds = spec.AcceptanceCriteria.Select(static criterion => criterion.Id).ToHashSet(StringComparer.Ordinal);
        foreach (var requirement in spec.Requirements.Concat(spec.NonFunctionalRequirements))
        {
            foreach (var link in requirement.AcceptanceCriteriaIds.Where(id => !criteriaIds.Contains(id)))
            {
                findings.Add(Create(
                    $"det-dangling-ac-{requirement.Id}-{link}",
                    FindingSeverity.Major,
                    RuleKeys.TraceabilityBreak,
                    $"Requirement {requirement.Id} links to acceptance criterion '{link}' which does not exist.",
                    [requirement.Id]));
            }
        }
    }

    private static bool CheckAcceptanceCriteria(ProjectSpec spec, List<Finding> findings)
    {
        var unverifiable = false;
        foreach (var criterion in spec.AcceptanceCriteria)
        {
            if (!string.IsNullOrWhiteSpace(criterion.Given)
                && !string.IsNullOrWhiteSpace(criterion.When)
                && !string.IsNullOrWhiteSpace(criterion.Then))
            {
                continue;
            }

            unverifiable = true;
            findings.Add(Create(
                $"det-unverifiable-{criterion.Id}",
                FindingSeverity.Critical,
                RuleKeys.UnverifiableAcceptanceCriterion,
                $"Acceptance criterion {criterion.Id} is missing a given, when or then clause.",
                [criterion.Id]));
        }

        return unverifiable;
    }

    private static void CheckMeasurements(ProjectSpec spec, List<Finding> findings)
    {
        foreach (var requirement in spec.NonFunctionalRequirements.Where(static requirement =>
                     string.IsNullOrWhiteSpace(requirement.Measurement)))
        {
            findings.Add(Create(
                $"det-nfr-measurement-{requirement.Id}",
                FindingSeverity.Major,
                RuleKeys.AmbiguousMetric,
                $"Non-functional requirement {requirement.Id} has no measured value or verification method.",
                [requirement.Id]));
        }

        foreach (var requirement in spec.Requirements.Concat(spec.NonFunctionalRequirements))
        {
            var word = AmbiguousWords.FirstOrDefault(candidate =>
                requirement.Statement.Contains(candidate, StringComparison.OrdinalIgnoreCase));
            if (word is null || requirement.Statement.Any(char.IsAsciiDigit))
            {
                continue;
            }

            findings.Add(Create(
                $"det-ambiguous-{requirement.Id}",
                FindingSeverity.Minor,
                RuleKeys.AmbiguousMetric,
                $"Requirement {requirement.Id} uses '{word}' without a measurable condition.",
                [requirement.Id]));
        }
    }

    private static void CheckNonGoals(ProjectSpec spec, List<Finding> findings)
    {
        foreach (var nonGoal in spec.NonGoals.Where(static nonGoal => nonGoal.Length >= 4))
        {
            foreach (var requirement in spec.Requirements.Concat(spec.NonFunctionalRequirements)
                         .Where(requirement =>
                             requirement.Title.Contains(nonGoal, StringComparison.OrdinalIgnoreCase)
                             || requirement.Statement.Contains(nonGoal, StringComparison.OrdinalIgnoreCase)))
            {
                findings.Add(Create(
                    $"det-nongoal-{requirement.Id}",
                    FindingSeverity.Critical,
                    RuleKeys.NonGoalViolation,
                    $"Requirement {requirement.Id} implements the declared non-goal '{nonGoal}'.",
                    [requirement.Id]));
            }
        }
    }

    private static void CheckStates(ProjectSpec spec, List<Finding> findings)
    {
        if (spec.Journeys.Count > 0 && spec.StateMatrix.Count == 0)
        {
            findings.Add(Create(
                "det-state-matrix-missing",
                FindingSeverity.Major,
                RuleKeys.MissingState,
                "The specification defines user journeys but no state matrix.",
                []));
        }

        foreach (var entry in spec.StateMatrix.Where(static entry => entry.NotApplicableReason is null))
        {
            var missing = new List<string>();
            if (string.IsNullOrWhiteSpace(entry.Loading))
            {
                missing.Add("loading");
            }

            if (string.IsNullOrWhiteSpace(entry.Empty))
            {
                missing.Add("empty");
            }

            if (string.IsNullOrWhiteSpace(entry.Failure))
            {
                missing.Add("error");
            }

            if (string.IsNullOrWhiteSpace(entry.Permission))
            {
                missing.Add("permission");
            }

            if (missing.Count == 0)
            {
                continue;
            }

            findings.Add(Create(
                $"det-state-{entry.Screen}",
                FindingSeverity.Major,
                RuleKeys.MissingState,
                $"Screen '{entry.Screen}' has no {string.Join(", ", missing)} state and no reason why it does not apply.",
                []));
        }
    }

    private static bool CheckTechnicalProfile(ProjectSpec spec, List<Finding> findings)
    {
        var technical = spec.Technical;
        var securityPresent = technical.Security.Count > 0;
        if (!securityPresent)
        {
            findings.Add(Create(
                "det-security-missing",
                FindingSeverity.Critical,
                RuleKeys.MissingAuthorization,
                "The technical profile has no authentication, authorization or data protection decision.",
                []));
        }

        if (technical.Observability.Count == 0 || technical.Deployment.Count == 0)
        {
            findings.Add(Create(
                "det-ops-missing",
                FindingSeverity.Major,
                RuleKeys.OperationsGap,
                "The technical profile is missing observability or deployment decisions.",
                []));
        }

        foreach (var contract in technical.ApiContracts.Where(static contract =>
                     contract.ErrorResponses.Count == 0 || string.IsNullOrWhiteSpace(contract.TimeoutAndRetry)))
        {
            findings.Add(Create(
                $"det-contract-{contract.Operation}",
                FindingSeverity.Major,
                RuleKeys.MissingFailureHandling,
                $"Contract '{contract.Operation}' does not define error responses and timeout or retry behaviour.",
                [.. contract.RequirementIds]));
        }

        var requirementIds = spec.Requirements.Concat(spec.NonFunctionalRequirements)
            .Select(static requirement => requirement.Id)
            .ToHashSet(StringComparer.Ordinal);
        foreach (var component in technical.Components.Where(component =>
                     component.RequirementIds.Count == 0
                     || component.RequirementIds.All(id => !requirementIds.Contains(id))))
        {
            findings.Add(Create(
                $"det-component-{component.Name}",
                FindingSeverity.Major,
                RuleKeys.UnjustifiedComponent,
                $"Component '{component.Name}' is not justified by any requirement.",
                []));
        }

        return securityPresent;
    }

    private static bool CheckOpenDecisions(ProjectSpec spec, List<Finding> findings)
    {
        var unresolved = false;
        foreach (var decision in spec.OpenDecisions.Where(static decision =>
                     string.IsNullOrWhiteSpace(decision.Resolution)))
        {
            if (decision.IsCritical)
            {
                unresolved = true;
            }

            findings.Add(Create(
                $"det-open-decision-{decision.Id}",
                decision.IsCritical ? FindingSeverity.Critical : FindingSeverity.Minor,
                RuleKeys.Other,
                $"Decision {decision.Id} is unresolved: {decision.Question}",
                []));
        }

        return unresolved;
    }

    private static bool CheckArtifactContent(DeterministicInput input, List<Finding> findings)
    {
        var detected = false;
        foreach (var (path, content) in AllArtifacts(input))
        {
            if (SecretPattern().IsMatch(content) || SourceCodeFencePattern().IsMatch(content))
            {
                detected = true;
                findings.Add(Create(
                    $"det-secret-{path}",
                    FindingSeverity.Critical,
                    RuleKeys.SecurityGap,
                    $"Artifact '{path}' contains a secret pattern or product implementation source code.",
                    []));
            }

            var placeholder = PlaceholderPatterns.FirstOrDefault(pattern =>
                content.Contains(pattern, StringComparison.OrdinalIgnoreCase));
            if (placeholder is not null)
            {
                findings.Add(Create(
                    $"det-placeholder-{path}",
                    FindingSeverity.Major,
                    RuleKeys.Other,
                    $"Artifact '{path}' still contains the placeholder '{placeholder}'.",
                    []));
            }

            if (content.Contains(MissingContentMarker, StringComparison.Ordinal))
            {
                findings.Add(Create(
                    $"det-empty-section-{path}",
                    FindingSeverity.Major,
                    RuleKeys.Other,
                    $"Artifact '{path}' has at least one required section without content.",
                    []));
            }
        }

        return detected;
    }

    private static (bool Valid, IReadOnlyList<string> MissingMustIds) CheckTargetFiles(
        DeterministicInput input,
        List<Finding> findings)
    {
        var valid = true;
        var mustIds = RequirementGraph.Build(input.Spec).MustRequirementIds();
        var missing = new List<string>();

        foreach (var file in input.TargetFiles)
        {
            foreach (var targetId in file.TargetIds)
            {
                if (!TargetCatalog.TryGet(targetId, out var profile))
                {
                    valid = false;
                    findings.Add(Create(
                        $"det-target-unknown-{targetId}",
                        FindingSeverity.Critical,
                        RuleKeys.TargetFileMismatch,
                        $"Target '{targetId}' is not in the support matrix.",
                        []));
                    continue;
                }

                if (!string.Equals(profile.Path, file.Path, StringComparison.Ordinal))
                {
                    valid = false;
                    findings.Add(Create(
                        $"det-target-path-{targetId}",
                        FindingSeverity.Critical,
                        RuleKeys.TargetFileMismatch,
                        $"Target '{targetId}' must be written to '{profile.Path}' but was written to '{file.Path}'.",
                        []));
                }

                if (profile.Frontmatter != FrontmatterKind.None && !HasFrontmatter(file.Content))
                {
                    valid = false;
                    findings.Add(Create(
                        $"det-target-frontmatter-{targetId}",
                        FindingSeverity.Critical,
                        RuleKeys.TargetFileMismatch,
                        $"Target '{targetId}' requires YAML frontmatter which is missing or malformed.",
                        []));
                }
            }

            foreach (var section in TargetFileRenderer.RequiredSections.Where(section =>
                         !file.Content.Contains($"## {section}", StringComparison.Ordinal)))
            {
                valid = false;
                findings.Add(Create(
                    $"det-target-section-{file.Path}-{section}",
                    FindingSeverity.Critical,
                    RuleKeys.TargetFileMismatch,
                    $"Target file '{file.Path}' is missing the required section '{section}'.",
                    []));
            }

            foreach (var document in new[] { DocumentRenderer.IdeationPath, DocumentRenderer.PrdPath, DocumentRenderer.TrdPath }
                         .Where(document => !file.Content.Contains(document, StringComparison.Ordinal)))
            {
                valid = false;
                findings.Add(Create(
                    $"det-target-reference-{file.Path}-{document}",
                    FindingSeverity.Critical,
                    RuleKeys.TargetFileMismatch,
                    $"Target file '{file.Path}' does not reference '{document}'.",
                    []));
            }

            missing.AddRange(mustIds.Where(id => !file.Content.Contains(id, StringComparison.Ordinal)));
        }

        var missingIds = missing.Distinct(StringComparer.Ordinal).OrderBy(static id => id, StringComparer.Ordinal).ToArray();
        foreach (var id in missingIds)
        {
            findings.Add(Create(
                $"det-target-must-{id}",
                FindingSeverity.Critical,
                RuleKeys.TargetFileMismatch,
                $"Must requirement {id} is missing from at least one target instruction file.",
                [id]));
        }

        return (valid && missingIds.Length == 0, missingIds);
    }

    private static bool CheckVersions(DeterministicInput input, List<Finding> findings)
    {
        var consistent = true;
        var label = input.Context.SpecVersion;

        foreach (var document in input.Documents.Where(document =>
                     !document.Content.Contains($"| Spec Version | {label} |", StringComparison.Ordinal)))
        {
            consistent = false;
            findings.Add(Create(
                $"det-version-{document.Path}",
                FindingSeverity.Critical,
                RuleKeys.TargetFileMismatch,
                $"Document '{document.Path}' does not declare spec version '{label}'.",
                []));
        }

        foreach (var file in input.TargetFiles.Where(file =>
                     !file.Content.Contains($"Spec Version: {label}", StringComparison.Ordinal)))
        {
            consistent = false;
            findings.Add(Create(
                $"det-version-{file.Path}",
                FindingSeverity.Critical,
                RuleKeys.TargetFileMismatch,
                $"Target file '{file.Path}' does not declare spec version '{label}'.",
                []));
        }

        return consistent;
    }

    private static void CheckLengthBudgets(DeterministicInput input, List<Finding> findings)
    {
        foreach (var (path, minimum, maximum) in LengthBudgets)
        {
            var document = input.Documents.FirstOrDefault(document =>
                string.Equals(document.Path, path, StringComparison.Ordinal));
            if (document is null)
            {
                findings.Add(Create(
                    $"det-document-missing-{path}",
                    FindingSeverity.Critical,
                    RuleKeys.Other,
                    $"Required document '{path}' was not rendered.",
                    []));
                continue;
            }

            var lines = document.Content.Split('\n').Length;
            if (lines < minimum || lines > maximum)
            {
                findings.Add(Create(
                    $"det-length-{path}",
                    FindingSeverity.Minor,
                    RuleKeys.Other,
                    $"Document '{path}' has {lines} lines, outside the {minimum} to {maximum} budget. Review duplication or splitting.",
                    []));
            }
        }
    }

    private static IEnumerable<(string Path, string Content)> AllArtifacts(DeterministicInput input) =>
        input.Documents.Select(static document => (document.Path, document.Content))
            .Concat(input.TargetFiles.Select(static file => (file.Path, file.Content)));

    private static bool HasFrontmatter(string content)
    {
        if (!content.StartsWith("---\n", StringComparison.Ordinal))
        {
            return false;
        }

        return content.IndexOf("\n---\n", 3, StringComparison.Ordinal) > 0;
    }

    private static Finding Create(
        string id,
        FindingSeverity severity,
        string ruleKey,
        string statement,
        IReadOnlyList<string> affectedIds) => new()
        {
            Id = id,
            Severity = severity,
            Category = "deterministic",
            RuleKey = ruleKey,
            Statement = statement,
            AffectedIds = affectedIds,
            Evidence = ["Deterministic check"],
            RequiresUserDecision = false
        };

    [GeneratedRegex(
        @"(gh[pousr]_[A-Za-z0-9]{20,})|(sk-[A-Za-z0-9]{20,})|(AKIA[0-9A-Z]{16})|((?i:password|client_secret|api[_-]?key|connectionstring)\s*[:=]\s*[""']?[A-Za-z0-9/+=_-]{8,})",
        RegexOptions.CultureInvariant)]
    private static partial Regex SecretPattern();

    [GeneratedRegex(@"```(csharp|cs|typescript|tsx|javascript|python|java|go|rust|php|ruby)\b", RegexOptions.CultureInvariant)]
    private static partial Regex SourceCodeFencePattern();
}
