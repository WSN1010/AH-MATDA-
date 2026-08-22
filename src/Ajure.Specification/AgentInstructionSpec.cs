namespace Ajure.Specification;

/// <summary>Shared instruction payload rendered into every target file (AI-FILE-SPEC 5).</summary>
public sealed record AgentInstructionSpec
{
    public required string SpecVersion { get; init; }

    public required string ProjectName { get; init; }

    public IReadOnlyList<string> TargetIds { get; init; } = [];

    public required string Mission { get; init; }

    public IReadOnlyList<string> SourceFiles { get; init; } =
        [DocumentRenderer.IdeationPath, DocumentRenderer.PrdPath, DocumentRenderer.TrdPath];

    public IReadOnlyList<string> Precedence { get; init; } = DefaultPrecedence;

    public IReadOnlyList<string> LockedDecisions { get; init; } = [];

    public IReadOnlyList<string> Scope { get; init; } = [];

    public IReadOnlyList<string> NonGoals { get; init; } = [];

    public IReadOnlyList<string> Workflow { get; init; } = DefaultWorkflow;

    public IReadOnlyList<string> QualityGates { get; init; } = [];

    public string AmbiguityPolicy { get; init; } = DefaultAmbiguityPolicy;

    public IReadOnlyList<string> SafetyRules { get; init; } = DefaultSafetyRules;

    public IReadOnlyList<string> DoneDefinition { get; init; } = DefaultDoneDefinition;

    /// <summary>
    /// Meaning hash over the fields that must not differ between targets (AI-FILE-SPEC 8).
    /// Tool specific syntax may differ, this value may not.
    /// </summary>
    public string SemanticFingerprint() => CanonicalJson.ComputeHash(new
    {
        mission = Mission,
        scope = Scope,
        nonGoals = NonGoals,
        lockedDecisions = LockedDecisions,
        qualityGates = QualityGates,
        doneDefinition = DoneDefinition
    });

    /// <summary>Builds the deterministic payload from the specification. No model output is involved.</summary>
    public static AgentInstructionSpec FromSpec(ProjectSpec spec, DocumentContext context)
    {
        ArgumentNullException.ThrowIfNull(spec);
        ArgumentNullException.ThrowIfNull(context);

        var requirements = spec.Requirements.Concat(spec.NonFunctionalRequirements).ToArray();
        var lockedDecisions = spec.LockedDecisions
            .Concat(spec.TechnicalDecisions
                .Where(static decision => decision.IsLocked)
                .Select(static decision => $"{decision.Id} {decision.Title}: {decision.Decision}"))
            .ToArray();

        return new AgentInstructionSpec
        {
            SpecVersion = context.SpecVersion,
            ProjectName = context.ProjectName,
            TargetIds = [.. TargetCatalog.Order(context.TargetIds).Select(static profile => profile.TargetId)],
            Mission = spec.Vision,
            LockedDecisions = lockedDecisions,
            Scope =
            [
                .. requirements
                    .Where(static requirement => requirement.Priority != Priority.Could)
                    .Select(static requirement =>
                        $"{requirement.Id} [{requirement.Priority}] {requirement.Title}: {requirement.Statement}")
            ],
            NonGoals = [.. spec.NonGoals],
            QualityGates = spec.Technical.TestingStrategy.Count == 0
                ? DefaultQualityGates
                : [.. spec.Technical.TestingStrategy, .. DefaultQualityGates],
            DoneDefinition =
            [
                .. DefaultDoneDefinition,
                .. spec.Release.BlockingConditions.Select(static condition => $"Release blocker resolved: {condition}")
            ]
        };
    }

    private static IReadOnlyList<string> DefaultPrecedence { get; } =
    [
        "The user request made at implementation time",
        "The safety, execution and verification rules in this file",
        $"{DocumentRenderer.PrdPath}: product behaviour and acceptance criteria",
        $"{DocumentRenderer.TrdPath}: technical and operational constraints",
        $"{DocumentRenderer.IdeationPath}: background and intent"
    ];

    private static IReadOnlyList<string> DefaultWorkflow { get; } =
    [
        "Read the three common documents and the current repository state.",
        "Map every requirement id and acceptance criterion to an implementation task.",
        "Complete vertical slices instead of scaffolding.",
        "Include loading, empty, error, permission and responsive states.",
        "Run the build, test and deployment verification defined in the TRD.",
        "Fix failures and run the verification again.",
        "State the implemented requirements and remaining limits in the final answer."
    ];

    private static IReadOnlyList<string> DefaultQualityGates { get; } =
    [
        "Build succeeds with no new warnings treated as errors",
        "Type checks pass",
        "Automated tests for every Must requirement pass",
        "Accessibility and responsive checks pass for user interface work"
    ];

    private static IReadOnlyList<string> DefaultSafetyRules { get; } =
    [
        "Do not replace the approved technology stack.",
        "Do not add authentication, payment, database or framework dependencies that the specification does not require.",
        "Never hardcode secrets in code or configuration.",
        "Do not revert existing user changes.",
        "Do not delete or weaken tests to make a run look successful.",
        "Do not implement anything listed as a non-goal."
    ];

    private static IReadOnlyList<string> DefaultDoneDefinition { get; } =
    [
        "Every Must requirement is implemented",
        "Every acceptance criterion has verification evidence",
        "Build, type check and tests pass",
        "The core user flows actually run",
        "User interfaces support desktop, mobile and the required states",
        "Errors are surfaced instead of hidden",
        "Documentation and run instructions are current",
        "Incomplete work is never reported as complete"
    ];

    private const string DefaultAmbiguityPolicy =
        "When the specification answers the question, implement it without asking. "
        + "When it does not, choose the smallest assumption that keeps every Must requirement intact and record the assumption in the final answer.";
}

public sealed record RenderedTargetFile
{
    public required string Path { get; init; }

    public required string Content { get; init; }

    /// <summary>Target ids covered by this file. More than one when tools share a path, for example Copilot and Codex.</summary>
    public required IReadOnlyList<string> TargetIds { get; init; }

    public string ContentHash => Specification.ContentHash.OfText(Content);
}

/// <summary>Renders the shared instruction payload into each target's native file (AI-FILE-SPEC 7).</summary>
public static class TargetFileRenderer
{
    public const string TemplateVersion = "1.0";

    /// <summary>Section titles every target file must contain (AI-FILE-SPEC 6).</summary>
    public static IReadOnlyList<string> RequiredSections { get; } =
    [
        "Mission",
        "Read Order",
        "Source Precedence",
        "Scope",
        "Non-goals",
        "Locked Decisions",
        "Implementation Workflow",
        "Quality Gates",
        "Guardrails",
        "Definition of Done"
    ];

    /// <summary>Renders one file per native path. Targets that share a path are merged into a single file.</summary>
    public static IReadOnlyList<RenderedTargetFile> RenderBundle(AgentInstructionSpec instruction)
    {
        ArgumentNullException.ThrowIfNull(instruction);

        var profiles = TargetCatalog.Order(instruction.TargetIds);
        return
        [
            .. profiles
                .GroupBy(static profile => profile.Path, StringComparer.Ordinal)
                .OrderBy(static group => group.Key, StringComparer.Ordinal)
                .Select(group => new RenderedTargetFile
                {
                    Path = group.Key,
                    Content = Render(instruction, group.First(), [.. group]),
                    TargetIds = [.. group.Select(static profile => profile.TargetId)]
                })
        ];
    }

    public static RenderedTargetFile Render(AgentInstructionSpec instruction, string targetId)
    {
        ArgumentNullException.ThrowIfNull(instruction);
        var profile = TargetCatalog.Get(targetId);
        return new RenderedTargetFile
        {
            Path = profile.Path,
            Content = Render(instruction, profile, [profile]),
            TargetIds = [profile.TargetId]
        };
    }

    private static string Render(
        AgentInstructionSpec instruction,
        TargetProfile profile,
        IReadOnlyList<TargetProfile> covered)
    {
        var writer = new MarkdownWriter();
        WriteFrontmatter(writer, profile);

        writer.Line($"# {instruction.ProjectName} implementation guide");
        writer.Blank();
        writer.Bullet($"Spec Version: {instruction.SpecVersion}");
        writer.Bullet($"Target: {string.Join(", ", covered.Select(static item => item.DisplayName))}");
        writer.Blank();

        writer.Heading("Mission");
        writer.Paragraph(instruction.Mission);
        writer.Bullet("Implement the acceptance criteria end to end. A scaffold is not a completed implementation.");
        writer.Bullet("Do not add features that the specification does not define.");
        writer.Blank();

        writer.Heading("Read Order");
        for (var index = 0; index < instruction.SourceFiles.Count; index++)
        {
            writer.Bullet($"{index + 1}. {FileReference(instruction.SourceFiles[index], profile)}");
        }

        writer.Bullet($"{instruction.SourceFiles.Count + 1}. {profile.Path}: execution and verification rules");
        writer.Blank();

        writer.Heading("Source Precedence");
        writer.NumberedList(instruction.Precedence);

        writer.Heading("Scope");
        writer.BulletList(instruction.Scope);

        writer.Heading("Non-goals");
        writer.BulletList(instruction.NonGoals);

        writer.Heading("Locked Decisions");
        writer.BulletList(instruction.LockedDecisions);

        writer.Heading("Implementation Workflow");
        writer.NumberedList(instruction.Workflow);

        writer.Heading("Quality Gates");
        writer.BulletList(instruction.QualityGates);

        writer.Heading("Guardrails");
        writer.BulletList(instruction.SafetyRules);
        writer.Bullet($"Ambiguity policy: {instruction.AmbiguityPolicy}");
        writer.Blank();

        writer.Heading("Definition of Done");
        writer.BulletList(instruction.DoneDefinition);

        if (covered.Count > 1 || !profile.IsNative)
        {
            writer.Heading("Tool-specific Notes");
            if (covered.Count > 1)
            {
                writer.Bullet(
                    "This file is shared by "
                    + string.Join(", ", covered.Select(static item => item.DisplayName))
                    + ". Use only Markdown that every listed tool understands.");
            }

            if (!profile.IsNative)
            {
                writer.Bullet(
                    "Generic fallback profile: confirm in your coding tool whether this file is discovered automatically.");
            }

            writer.Blank();
        }

        return writer.ToString();
    }

    private static void WriteFrontmatter(MarkdownWriter writer, TargetProfile profile)
    {
        switch (profile.Frontmatter)
        {
            case FrontmatterKind.CursorAlwaysApply:
                writer.Line("---");
                writer.Line("description: Ajure generated project rule");
                writer.Line("alwaysApply: true");
                writer.Line("---");
                writer.Blank();
                break;
            case FrontmatterKind.DevinAlwaysOn:
                writer.Line("---");
                writer.Line("trigger: always_on");
                writer.Line("---");
                writer.Blank();
                break;
            case FrontmatterKind.None:
            default:
                break;
        }
    }

    private static string FileReference(string path, TargetProfile profile)
    {
        if (!profile.SupportsImports)
        {
            return path;
        }

        return string.Equals(profile.TargetId, TargetCatalog.GeminiCli, StringComparison.Ordinal)
            ? $"@./{path}"
            : $"@{path}";
    }
}

internal static class MarkdownWriterExtensions
{
    public static void Heading(this MarkdownWriter writer, string title)
    {
        writer.Blank();
        writer.Line($"## {title}");
        writer.Blank();
    }

    public static void NumberedList(this MarkdownWriter writer, IEnumerable<string> items)
    {
        var index = 0;
        foreach (var item in items)
        {
            index++;
            writer.Line($"{index}. {item}");
        }

        if (index == 0)
        {
            writer.Line("1. _Not provided in the current specification._");
        }

        writer.Blank();
    }
}
