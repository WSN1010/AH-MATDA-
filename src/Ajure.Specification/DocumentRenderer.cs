using System.Globalization;
using System.Text;

namespace Ajure.Specification;

public sealed record DocumentContext
{
    public required string ProjectName { get; init; }

    /// <summary>Immutable version label shared by every artifact, for example <c>v3</c>.</summary>
    public required string SpecVersion { get; init; }

    public required SpecStatus Status { get; init; }

    public IReadOnlyList<string> TargetIds { get; init; } = [];

    public required DateTimeOffset GeneratedAt { get; init; }

    public string Source { get; init; } = "Ajure ProjectSpec";
}

public sealed record RenderedDocument
{
    public required string Path { get; init; }

    public required string Content { get; init; }

    public string ContentHash => Specification.ContentHash.OfText(Content);
}

/// <summary>Required section titles per DOCUMENT-SPEC, in rendering order.</summary>
public static class DocumentSections
{
    public static IReadOnlyList<string> Ideation { get; } =
    [
        "One-line Concept",
        "Problem",
        "Target Users and JTBD",
        "Evidence and Assumptions",
        "Options Considered",
        "Value Proposition",
        "Scope",
        "Risks",
        "Success Definition",
        "Locked Decisions"
    ];

    public static IReadOnlyList<string> Prd { get; } =
    [
        "Product Overview",
        "Goals and Non-goals",
        "Personas",
        "User Journeys",
        "Functional Requirements",
        "State Matrix",
        "Non-functional Requirements",
        "Business Rules",
        "Analytics",
        "Acceptance Criteria",
        "Traceability Matrix",
        "Release Scope"
    ];

    public static IReadOnlyList<string> Trd { get; } =
    [
        "Technical Scope and Constraints",
        "Architecture",
        "Components",
        "Repository Structure",
        "Domain and Data",
        "API and Integration Contracts",
        "State and Workflow",
        "Security and Privacy",
        "Reliability",
        "Observability",
        "Deployment",
        "Testing Strategy",
        "Technical Decisions",
        "Technical Traceability",
        "Known Risks and Implementation Order"
    ];
}

/// <summary>Deterministic ProjectSpec to Markdown rendering (TRD 7.1). Identifiers and paths come from here, never from a model.</summary>
public static class DocumentRenderer
{
    public const string IdeationPath = "IDEATION.md";
    public const string PrdPath = "PRD.md";
    public const string TrdPath = "TRD.md";

    private const string Missing = "_Not provided in the current specification._";

    public static IReadOnlyList<RenderedDocument> RenderAll(ProjectSpec spec, DocumentContext context) =>
    [
        RenderIdeation(spec, context),
        RenderPrd(spec, context),
        RenderTrd(spec, context)
    ];

    public static RenderedDocument RenderIdeation(ProjectSpec spec, DocumentContext context)
    {
        ArgumentNullException.ThrowIfNull(spec);
        ArgumentNullException.ThrowIfNull(context);

        var writer = new MarkdownWriter();
        WriteHeader(writer, context, "IDEATION");

        writer.Section(1, DocumentSections.Ideation[0]);
        writer.Paragraph(spec.Vision);

        writer.Section(2, DocumentSections.Ideation[1]);
        writer.Paragraph(spec.Problem);

        writer.Section(3, DocumentSections.Ideation[2]);
        if (spec.Personas.Count == 0)
        {
            writer.Paragraph(Missing);
        }

        foreach (var persona in spec.Personas)
        {
            writer.SubSection($"{persona.Id} {persona.Name}");
            writer.Bullet($"Priority: {(persona.IsPrimary ? "Primary" : "Secondary")}");
            writer.Bullet($"Situation: {Value(persona.Situation)}");
            writer.Bullet($"Motivation: {Value(persona.Motivation)}");
            writer.Bullet($"Expected outcome: {Value(persona.ExpectedOutcome)}");
            writer.Blank();
        }

        writer.Section(4, DocumentSections.Ideation[3]);
        writer.Table(
            ["Statement", "Type", "Verification"],
            spec.Evidence.Select(static item => new[]
            {
                item.Statement,
                item.IsVerified ? "Verified fact" : "Assumption",
                Value(item.VerificationMethod)
            }));

        writer.Section(5, DocumentSections.Ideation[4]);
        writer.Table(
            ["Option", "Summary", "Outcome"],
            spec.OptionsConsidered.Select(static option => new[]
            {
                option.Title,
                option.Summary,
                option.IsChosen ? "Chosen" : $"Rejected: {Value(option.RejectionReason)}"
            }));

        writer.Section(6, DocumentSections.Ideation[5]);
        writer.BulletList(spec.ValuePropositions);

        writer.Section(7, DocumentSections.Ideation[6]);
        writer.SubSection("MVP Must");
        writer.BulletList(RequirementTitles(spec, Priority.Must));
        writer.SubSection("Should and Could");
        writer.BulletList([.. RequirementTitles(spec, Priority.Should), .. RequirementTitles(spec, Priority.Could)]);
        writer.SubSection("Non-goals");
        writer.BulletList(spec.NonGoals);

        writer.Section(8, DocumentSections.Ideation[7]);
        writer.Table(
            ["ID", "Risk", "Likelihood", "Impact", "Mitigation"],
            spec.Risks.Select(static risk => new[]
            {
                risk.Id,
                risk.Statement,
                risk.Likelihood.ToString(),
                risk.Impact.ToString(),
                Value(risk.Mitigation)
            }));

        writer.Section(9, DocumentSections.Ideation[8]);
        writer.Table(
            ["Metric", "Target", "Kind"],
            spec.SuccessMetrics.Select(static metric => new[]
            {
                metric.Name,
                metric.Target,
                metric.Kind == MetricKind.UserOutcome ? "User outcome" : "Product and operations"
            }));

        writer.Section(10, DocumentSections.Ideation[9]);
        writer.BulletList(spec.LockedDecisions);

        return new RenderedDocument { Path = IdeationPath, Content = writer.ToString() };
    }

    public static RenderedDocument RenderPrd(ProjectSpec spec, DocumentContext context)
    {
        ArgumentNullException.ThrowIfNull(spec);
        ArgumentNullException.ThrowIfNull(context);

        var writer = new MarkdownWriter();
        WriteHeader(writer, context, "PRD");

        writer.Section(1, DocumentSections.Prd[0]);
        writer.Paragraph(spec.Vision);
        writer.Bullet($"Release scope: {Join(spec.Release.Mvp)}");
        writer.Blank();

        writer.Section(2, DocumentSections.Prd[1]);
        writer.Table(
            ["ID", "Goal", "Success metric"],
            spec.Goals.Select(static goal => new[] { goal.Id, goal.Statement, Value(goal.SuccessMetric) }));
        writer.SubSection("Non-goals");
        writer.BulletList(spec.NonGoals);

        writer.Section(3, DocumentSections.Prd[2]);
        writer.Table(
            ["ID", "Persona", "Goal", "Environment and constraints"],
            spec.Personas.Select(static persona => new[]
            {
                persona.Id,
                persona.Name,
                Value(persona.ExpectedOutcome),
                Value(persona.Constraints)
            }));

        writer.Section(4, DocumentSections.Prd[3]);
        if (spec.Journeys.Count == 0)
        {
            writer.Paragraph(Missing);
        }

        foreach (var journey in spec.Journeys)
        {
            writer.SubSection($"{journey.Id} {journey.Title}");
            writer.Bullet($"Entry: {Value(journey.Entry)}");
            writer.Bullet($"Steps: {Join(journey.Steps)}");
            writer.Bullet($"Success exit: {Value(journey.SuccessExit)}");
            writer.Bullet($"Failure paths: {Join(journey.FailurePaths)}");
            writer.Bullet($"Requirements: {Join(journey.RequirementIds)}");
            writer.Blank();
        }

        writer.Section(5, DocumentSections.Prd[4]);
        WriteRequirements(writer, spec.Requirements);

        writer.Section(6, DocumentSections.Prd[5]);
        writer.Table(
            ["Screen", "Loading", "Empty", "Error", "Success", "Disabled", "Permission", "Not applicable"],
            spec.StateMatrix.Select(static entry => new[]
            {
                entry.Screen,
                Value(entry.Loading),
                Value(entry.Empty),
                Value(entry.Failure),
                Value(entry.Success),
                Value(entry.Disabled),
                Value(entry.Permission),
                Value(entry.NotApplicableReason)
            }));

        writer.Section(7, DocumentSections.Prd[6]);
        WriteRequirements(writer, spec.NonFunctionalRequirements);

        writer.Section(8, DocumentSections.Prd[7]);
        writer.Table(
            ["Precedence", "Rule"],
            spec.BusinessRules
                .OrderBy(static rule => rule.Precedence)
                .ThenBy(static rule => rule.Statement, StringComparer.Ordinal)
                .Select(static rule => new[] { rule.Precedence.ToString(CultureInfo.InvariantCulture), rule.Statement }));

        writer.Section(9, DocumentSections.Prd[8]);
        writer.Table(
            ["Event", "Properties", "Purpose"],
            spec.AnalyticsEvents.Select(static analyticsEvent => new[]
            {
                analyticsEvent.Name,
                Join(analyticsEvent.Properties),
                Value(analyticsEvent.Purpose)
            }));

        writer.Section(10, DocumentSections.Prd[9]);
        writer.Table(
            ["ID", "Given", "When", "Then", "Verification", "Requirements"],
            spec.AcceptanceCriteria.Select(static criterion => new[]
            {
                criterion.Id,
                criterion.Given,
                criterion.When,
                criterion.Then,
                criterion.VerificationType.ToString(),
                Join(criterion.RequirementIds)
            }));

        writer.Section(11, DocumentSections.Prd[10]);
        writer.SubSection("Requirement to acceptance criteria");
        writer.Table(
            ["Requirement", "Priority", "Acceptance criteria"],
            spec.Requirements.Concat(spec.NonFunctionalRequirements).Select(static requirement => new[]
            {
                requirement.Id,
                requirement.Priority.ToString(),
                Join(requirement.AcceptanceCriteriaIds)
            }));
        writer.SubSection("Journey to requirements");
        writer.Table(
            ["Journey", "Requirements"],
            spec.Journeys.Select(static journey => new[] { journey.Id, Join(journey.RequirementIds) }));

        writer.Section(12, DocumentSections.Prd[11]);
        writer.SubSection("MVP");
        writer.BulletList(spec.Release.Mvp);
        writer.SubSection("Later");
        writer.BulletList(spec.Release.Later);
        writer.SubSection("Release blocking conditions");
        writer.BulletList(spec.Release.BlockingConditions);

        return new RenderedDocument { Path = PrdPath, Content = writer.ToString() };
    }

    public static RenderedDocument RenderTrd(ProjectSpec spec, DocumentContext context)
    {
        ArgumentNullException.ThrowIfNull(spec);
        ArgumentNullException.ThrowIfNull(context);

        var technical = spec.Technical;
        var writer = new MarkdownWriter();
        WriteHeader(writer, context, "TRD");

        writer.Section(1, DocumentSections.Trd[0]);
        writer.SubSection("Constraints");
        writer.BulletList(technical.Constraints);
        writer.SubSection("Must technologies");
        writer.BulletList(technical.MustTechnologies);
        writer.SubSection("Forbidden choices");
        writer.BulletList(technical.ForbiddenChoices);

        writer.Section(2, DocumentSections.Trd[1]);
        writer.Paragraph(technical.Architecture);
        writer.SubSection("Trust boundaries");
        writer.BulletList(technical.TrustBoundaries);

        writer.Section(3, DocumentSections.Trd[2]);
        writer.Table(
            ["Component", "Responsibility", "Dependencies", "Requirements"],
            technical.Components.Select(static component => new[]
            {
                component.Name,
                component.Responsibility,
                Join(component.Dependencies),
                Join(component.RequirementIds)
            }));

        writer.Section(4, DocumentSections.Trd[3]);
        writer.Table(
            ["Path", "Ownership"],
            technical.RepositoryStructure.Select(static area => new[] { area.Path, area.Ownership }));

        writer.Section(5, DocumentSections.Trd[4]);
        writer.Table(
            ["Entity", "Fields", "Relationships", "Retention"],
            technical.DataEntities.Select(static entity => new[]
            {
                entity.Name,
                Join(entity.Fields),
                Join(entity.Relationships),
                Value(entity.Retention)
            }));

        writer.Section(6, DocumentSections.Trd[5]);
        if (technical.ApiContracts.Count == 0)
        {
            writer.Paragraph(Missing);
        }

        foreach (var contract in technical.ApiContracts)
        {
            writer.SubSection(contract.Operation);
            writer.Bullet($"Purpose: {contract.Purpose}");
            writer.Bullet($"Auth: {Value(contract.Auth)}");
            writer.Bullet($"Request: {Value(contract.Request)}");
            writer.Bullet($"Success response: {Value(contract.SuccessResponse)}");
            writer.Bullet($"Error responses: {Join(contract.ErrorResponses)}");
            writer.Bullet($"Idempotency: {Value(contract.Idempotency)}");
            writer.Bullet($"Timeout and retry: {Value(contract.TimeoutAndRetry)}");
            writer.Bullet($"Requirements: {Join(contract.RequirementIds)}");
            writer.Blank();
        }

        writer.Section(7, DocumentSections.Trd[6]);
        writer.Table(
            ["State", "Allowed transitions", "Failure handling"],
            technical.States.Select(static state => new[]
            {
                state.Name,
                Join(state.AllowedTransitions),
                Value(state.FailureHandling)
            }));

        writer.Section(8, DocumentSections.Trd[7]);
        writer.BulletList(technical.Security);

        writer.Section(9, DocumentSections.Trd[8]);
        writer.BulletList(technical.Reliability);

        writer.Section(10, DocumentSections.Trd[9]);
        writer.BulletList(technical.Observability);

        writer.Section(11, DocumentSections.Trd[10]);
        writer.BulletList(technical.Deployment);

        writer.Section(12, DocumentSections.Trd[11]);
        writer.BulletList(technical.TestingStrategy);

        writer.Section(13, DocumentSections.Trd[12]);
        if (spec.TechnicalDecisions.Count == 0)
        {
            writer.Paragraph(Missing);
        }

        foreach (var decision in spec.TechnicalDecisions)
        {
            writer.SubSection($"{decision.Id} {decision.Title}");
            writer.Bullet($"Decision: {decision.Decision}");
            writer.Bullet($"Rationale: {Value(decision.Rationale)}");
            writer.Bullet($"Alternatives: {Join(decision.Alternatives)}");
            writer.Bullet($"Requirements: {Join(decision.RequirementIds)}");
            writer.Bullet($"Locked: {(decision.IsLocked ? "yes" : "no")}");
            writer.Blank();
        }

        writer.Section(14, DocumentSections.Trd[13]);
        var componentsByRequirement = technical.Components
            .SelectMany(component => component.RequirementIds.Select(id => (Id: id, component.Name)))
            .GroupBy(static pair => pair.Id, StringComparer.Ordinal)
            .ToDictionary(
                static group => group.Key,
                static group => group.Select(static pair => pair.Name).ToArray(),
                StringComparer.Ordinal);
        writer.Table(
            ["Requirement", "Components", "Technical decisions", "Acceptance criteria"],
            spec.Requirements.Concat(spec.NonFunctionalRequirements).Select(requirement => new[]
            {
                requirement.Id,
                componentsByRequirement.TryGetValue(requirement.Id, out var names) ? Join(names) : Value(null),
                requirement.NoTechnicalImpact && requirement.TechnicalDecisionIds.Count == 0
                    ? "No technical impact"
                    : Join(requirement.TechnicalDecisionIds),
                Join(requirement.AcceptanceCriteriaIds)
            }));

        writer.Section(15, DocumentSections.Trd[14]);
        writer.SubSection("Known risks");
        writer.BulletList(spec.Risks.Select(static risk => $"{risk.Id}: {risk.Statement} (mitigation: {Value(risk.Mitigation)})"));
        writer.SubSection("Implementation order");
        writer.BulletList(technical.ImplementationOrder);

        return new RenderedDocument { Path = TrdPath, Content = writer.ToString() };
    }

    private static void WriteRequirements(MarkdownWriter writer, IReadOnlyList<Requirement> requirements)
    {
        if (requirements.Count == 0)
        {
            writer.Paragraph(Missing);
            return;
        }

        foreach (var requirement in requirements)
        {
            writer.SubSection($"{requirement.Id} [{requirement.Priority}] {requirement.Title}");
            writer.Bullet($"Statement: {requirement.Statement}");
            writer.Bullet($"Rationale: {Value(requirement.Rationale)}");
            if (requirement.Measurement is { Length: > 0 })
            {
                writer.Bullet($"Measurement: {requirement.Measurement}");
            }

            writer.Bullet($"Journeys: {Join(requirement.JourneyIds)}");
            writer.Bullet($"Acceptance: {Join(requirement.AcceptanceCriteriaIds)}");
            writer.Bullet(requirement.NoTechnicalImpact && requirement.TechnicalDecisionIds.Count == 0
                ? "Technical decisions: no technical impact"
                : $"Technical decisions: {Join(requirement.TechnicalDecisionIds)}");
            writer.Blank();
        }
    }

    private static void WriteHeader(MarkdownWriter writer, DocumentContext context, string documentName)
    {
        writer.Line($"# {context.ProjectName} - {documentName}");
        writer.Blank();
        writer.Table(
            ["Field", "Value"],
            [
                ["Project", context.ProjectName],
                ["Spec Version", context.SpecVersion],
                ["Status", context.Status.ToString()],
                ["Targets", Join(context.TargetIds)],
                ["Generated At", context.GeneratedAt.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture)],
                ["Source", context.Source]
            ]);
    }

    private static IReadOnlyList<string> RequirementTitles(ProjectSpec spec, Priority priority) =>
    [
        .. spec.Requirements.Concat(spec.NonFunctionalRequirements)
            .Where(requirement => requirement.Priority == priority)
            .Select(static requirement => $"{requirement.Id} {requirement.Title}")
    ];

    private static string Value(string? value) => string.IsNullOrWhiteSpace(value) ? "-" : value;

    private static string Join(IEnumerable<string> values)
    {
        var joined = string.Join(", ", values);
        return joined.Length == 0 ? "-" : joined;
    }
}

internal sealed class MarkdownWriter
{
    private readonly StringBuilder _builder = new();

    public void Line(string text) => _builder.Append(text).Append('\n');

    public void Blank()
    {
        if (_builder.Length >= 2 && _builder[^1] == '\n' && _builder[^2] == '\n')
        {
            return;
        }

        _builder.Append('\n');
    }

    public void Section(int number, string title)
    {
        Blank();
        Line(string.Create(CultureInfo.InvariantCulture, $"## {number}. {title}"));
        Blank();
    }

    public void SubSection(string title)
    {
        Blank();
        Line($"### {title}");
        Blank();
    }

    public void Paragraph(string text)
    {
        Line(string.IsNullOrWhiteSpace(text) ? "_Not provided in the current specification._" : text);
        Blank();
    }

    public void Bullet(string text) => Line($"- {text}");

    public void BulletList(IEnumerable<string> items)
    {
        var any = false;
        foreach (var item in items)
        {
            Bullet(item);
            any = true;
        }

        if (!any)
        {
            Line("- _Not provided in the current specification._");
        }

        Blank();
    }

    public void Table(IReadOnlyList<string> headers, IEnumerable<IReadOnlyList<string>> rows)
    {
        Line("| " + string.Join(" | ", headers) + " |");
        Line("|" + string.Concat(Enumerable.Repeat("---|", headers.Count)));
        var any = false;
        foreach (var row in rows)
        {
            Line("| " + string.Join(" | ", row.Select(Escape)) + " |");
            any = true;
        }

        if (!any)
        {
            Line("| " + string.Join(" | ", Enumerable.Repeat("-", headers.Count)) + " |");
        }

        Blank();
    }

    public override string ToString() => _builder.ToString();

    private static string Escape(string value) =>
        value.Replace("|", "\\|", StringComparison.Ordinal).Replace("\n", " ", StringComparison.Ordinal);
}
