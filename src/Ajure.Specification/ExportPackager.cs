using System.IO.Compression;
using System.Text;

namespace Ajure.Specification;

public sealed record ExportFile
{
    /// <summary>Path inside the ZIP, relative, forward slashes, for example <c>.cursor/rules/ajure.mdc</c>.</summary>
    public required string Path { get; init; }

    public required string Content { get; init; }
}

public sealed record ExportManifestFile
{
    public required string Path { get; init; }

    public required string Sha256 { get; init; }
}

public sealed record ExportManifest
{
    public required string SpecVersion { get; init; }

    public required string SpecHash { get; init; }

    public required IReadOnlyList<ExportManifestFile> Files { get; init; }

    public required IReadOnlyList<string> TargetIds { get; init; }

    public required IReadOnlyList<string> EvaluationModelIds { get; init; }

    public required bool IsSimulated { get; init; }

    public required string GeneratedAt { get; init; }

    public string TemplateVersion { get; init; } = TargetFileRenderer.TemplateVersion;
}

public sealed record ExportRequest
{
    public required string SpecVersion { get; init; }

    public required string SpecHash { get; init; }

    public required IReadOnlyList<ExportFile> Files { get; init; }

    public IReadOnlyList<string> TargetIds { get; init; } = [];

    public IReadOnlyList<string> EvaluationModelIds { get; init; } = [];

    public bool IsSimulated { get; init; }

    public required DateTimeOffset GeneratedAt { get; init; }
}

public sealed record ExportResult
{
    public required ReadOnlyMemory<byte> Content { get; init; }

    /// <summary>SHA-256 of the ZIP bytes.</summary>
    public required string Sha256 { get; init; }

    public required ExportManifest Manifest { get; init; }
}

/// <summary>
/// Builds the single ZIP deliverable (FR-013). Entry order, timestamps and the manifest are fixed,
/// so identical inputs always produce identical bytes.
/// </summary>
public static class ExportPackager
{
    public const string ManifestPath = "ajure-manifest.json";

    private static readonly DateTimeOffset ZipEpoch = new(1980, 1, 1, 0, 0, 0, TimeSpan.Zero);

    public static ExportResult Create(ExportRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Files.Count == 0)
        {
            throw new ArgumentException("An export needs at least one file.", nameof(request));
        }

        var duplicate = request.Files
            .GroupBy(static file => file.Path, StringComparer.Ordinal)
            .FirstOrDefault(static group => group.Count() > 1);
        if (duplicate is not null)
        {
            throw new ArgumentException($"Duplicate export path '{duplicate.Key}'.", nameof(request));
        }

        var files = request.Files
            .Select(static file => new { file.Path, Bytes = Encoding.UTF8.GetBytes(file.Content) })
            .OrderBy(static file => file.Path, StringComparer.Ordinal)
            .ToArray();

        var manifest = new ExportManifest
        {
            SpecVersion = request.SpecVersion,
            SpecHash = request.SpecHash,
            Files =
            [
                .. files.Select(static file => new ExportManifestFile
                {
                    Path = file.Path,
                    Sha256 = ContentHash.OfBytes(file.Bytes)
                })
            ],
            TargetIds = [.. TargetCatalog.Order(request.TargetIds).Select(static profile => profile.TargetId)],
            EvaluationModelIds = [.. request.EvaluationModelIds.Distinct(StringComparer.Ordinal).OrderBy(static id => id, StringComparer.Ordinal)],
            IsSimulated = request.IsSimulated,
            GeneratedAt = request.GeneratedAt.ToUniversalTime().ToString("O", System.Globalization.CultureInfo.InvariantCulture)
        };

        var entries = files
            .Select(static file => (file.Path, file.Bytes))
            .Append((Path: ManifestPath, Bytes: CanonicalJson.SerializeToUtf8Bytes(manifest)))
            .OrderBy(static entry => entry.Path, StringComparer.Ordinal)
            .ToArray();

        using var buffer = new MemoryStream();
        using (var archive = new ZipArchive(buffer, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var (path, bytes) in entries)
            {
                var entry = archive.CreateEntry(path.Replace('\\', '/'), CompressionLevel.Optimal);
                entry.LastWriteTime = ZipEpoch;
                using var stream = entry.Open();
                stream.Write(bytes, 0, bytes.Length);
            }
        }

        var zipBytes = buffer.ToArray();
        return new ExportResult
        {
            Content = zipBytes,
            Sha256 = ContentHash.OfBytes(zipBytes),
            Manifest = manifest
        };
    }
}
