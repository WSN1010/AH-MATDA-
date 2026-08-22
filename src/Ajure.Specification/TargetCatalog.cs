namespace Ajure.Specification;

public enum FrontmatterKind
{
    None,
    CursorAlwaysApply,
    DevinAlwaysOn
}

public sealed record TargetProfile
{
    public required string TargetId { get; init; }

    public required string DisplayName { get; init; }

    /// <summary>Native discovery path, relative to the repository root, always using forward slashes.</summary>
    public required string Path { get; init; }

    public required FrontmatterKind Frontmatter { get; init; }

    /// <summary>False for the generic fallback, which cannot claim native discovery.</summary>
    public required bool IsNative { get; init; }

    /// <summary>True when the tool resolves <c>@file.md</c> style imports.</summary>
    public bool SupportsImports { get; init; }
}

/// <summary>Support matrix from AI-FILE-SPEC 3. Catalog order is the deterministic rendering order.</summary>
public static class TargetCatalog
{
    public const string ClaudeCode = "claude-code";
    public const string GitHubCopilot = "github-copilot";
    public const string OpenAiCodex = "openai-codex";
    public const string GeminiCli = "gemini-cli";
    public const string Cursor = "cursor";
    public const string DevinWindsurf = "devin-windsurf";
    public const string Cline = "cline";
    public const string AmazonQ = "amazon-q";
    public const string Generic = "generic";

    private static readonly TargetProfile[] Profiles =
    [
        new()
        {
            TargetId = ClaudeCode,
            DisplayName = "Claude Code",
            Path = "CLAUDE.md",
            Frontmatter = FrontmatterKind.None,
            IsNative = true,
            SupportsImports = true
        },
        new()
        {
            TargetId = GitHubCopilot,
            DisplayName = "GitHub Copilot",
            Path = "AGENTS.md",
            Frontmatter = FrontmatterKind.None,
            IsNative = true
        },
        new()
        {
            TargetId = OpenAiCodex,
            DisplayName = "OpenAI Codex",
            Path = "AGENTS.md",
            Frontmatter = FrontmatterKind.None,
            IsNative = true
        },
        new()
        {
            TargetId = GeminiCli,
            DisplayName = "Gemini CLI",
            Path = "GEMINI.md",
            Frontmatter = FrontmatterKind.None,
            IsNative = true,
            SupportsImports = true
        },
        new()
        {
            TargetId = Cursor,
            DisplayName = "Cursor",
            Path = ".cursor/rules/ajure.mdc",
            Frontmatter = FrontmatterKind.CursorAlwaysApply,
            IsNative = true
        },
        new()
        {
            TargetId = DevinWindsurf,
            DisplayName = "Devin Desktop / Windsurf Cascade",
            Path = ".devin/rules/ajure.md",
            Frontmatter = FrontmatterKind.DevinAlwaysOn,
            IsNative = true
        },
        new()
        {
            TargetId = Cline,
            DisplayName = "Cline",
            Path = ".clinerules/ajure.md",
            Frontmatter = FrontmatterKind.None,
            IsNative = true
        },
        new()
        {
            TargetId = AmazonQ,
            DisplayName = "Amazon Q Developer",
            Path = ".amazonq/rules/ajure.md",
            Frontmatter = FrontmatterKind.None,
            IsNative = true
        },
        new()
        {
            TargetId = Generic,
            DisplayName = "Generic AGENTS.md profile",
            Path = "AGENTS.md",
            Frontmatter = FrontmatterKind.None,
            IsNative = false
        }
    ];

    public static IReadOnlyList<TargetProfile> All => Profiles;

    public static TargetProfile Get(string targetId) =>
        TryGet(targetId, out var profile)
            ? profile
            : throw new ArgumentOutOfRangeException(nameof(targetId), targetId, "Unknown target id.");

    public static bool TryGet(string? targetId, out TargetProfile profile)
    {
        foreach (var candidate in Profiles)
        {
            if (string.Equals(candidate.TargetId, targetId, StringComparison.Ordinal))
            {
                profile = candidate;
                return true;
            }
        }

        profile = Profiles[^1];
        return false;
    }

    public static string PathFor(string targetId) => Get(targetId).Path;

    /// <summary>Catalog order for the requested targets, duplicates removed.</summary>
    public static IReadOnlyList<TargetProfile> Order(IEnumerable<string> targetIds)
    {
        ArgumentNullException.ThrowIfNull(targetIds);
        var requested = targetIds.ToHashSet(StringComparer.Ordinal);
        return [.. Profiles.Where(profile => requested.Contains(profile.TargetId))];
    }
}
