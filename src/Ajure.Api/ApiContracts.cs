namespace Ajure.Api;

public sealed record CreateProjectRequest(string Name, string Idea, string? Locale);

public sealed record UpdateDecisionRequest(string Answer);

public sealed record CreateVersionRequest(
    Guid? BaseVersionId,
    string? GenerationProfile,
    string[]? TargetIds);

public sealed record UpdateArtifactRequest(string Content);

public sealed record JobAcceptedResponse(Guid JobId);
