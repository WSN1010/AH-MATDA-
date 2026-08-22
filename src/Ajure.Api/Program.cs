using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;
using Ajure.Api;
using Ajure.Infrastructure;
using Ajure.Specification;

var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();
builder.Services.AddProblemDetails(options =>
{
    options.CustomizeProblemDetails = context =>
    {
        context.ProblemDetails.Extensions.TryAdd("code", "request_error");
        context.ProblemDetails.Extensions.TryAdd(
            "message",
            context.ProblemDetails.Title ?? "The request was rejected.");
        context.ProblemDetails.Extensions.TryAdd(
            "correlationId",
            context.HttpContext.TraceIdentifier);
        context.ProblemDetails.Extensions.TryAdd("retryable", false);
        context.ProblemDetails.Extensions.TryAdd("details", null);
    };
});
builder.Services.AddExceptionHandler<ApiExceptionHandler>();
builder.Services.AddCors();
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.AddAjureStorage();

var app = builder.Build();

app.UseExceptionHandler();
if (app.Environment.IsDevelopment())
{
    app.UseCors(policy => policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod());
}

app.MapDefaultEndpoints();
app.MapGet("/", () => Results.Ok(new { service = "Ajure.Api", status = "ok" }));

var api = app.MapGroup("/api");

api.MapPost("/projects", async (
    CreateProjectRequest request,
    HttpContext context,
    AjureStore store,
    CancellationToken cancellationToken) =>
{
    var name = request.Name?.Trim();
    var idea = ApiResponseMapper.ParseIdea(request.Idea);
    var storedIdea = ApiResponseMapper.SerializeIdea(idea);
    if (string.IsNullOrWhiteSpace(name) || name.Length > 120)
    {
        return ApiProblems.Validation(
            context,
            "invalid_project_name",
            "Project name must contain 1 to 120 characters.");
    }

    if (string.IsNullOrWhiteSpace(idea.Summary) || storedIdea.Length > 20_000)
    {
        return ApiProblems.Validation(
            context,
            "invalid_project_idea",
            "Project idea must contain 1 to 20,000 characters.");
    }

    var locale = string.IsNullOrWhiteSpace(request.Locale) ? "ko-KR" : request.Locale.Trim();
    if (locale.Length > 35)
    {
        return ApiProblems.Validation(
            context,
            "invalid_project_locale",
            "Project locale must contain at most 35 characters.");
    }

    var targets = NormalizeTargets(request.TargetIds);
    if (targets is null)
    {
        return ApiProblems.Validation(
            context,
            "invalid_target",
            "One or more target IDs are not supported.");
    }

    var now = DateTimeOffset.UtcNow;
    var project = new ProjectRecord(
        Guid.NewGuid(),
        name,
        "local",
        locale,
        storedIdea,
        now);
    await store.CreateProjectAsync(project, cancellationToken).ConfigureAwait(false);
    var hashInput = $"{storedIdea}\nbalanced\n{string.Join(',', targets)}";
    var version = new SpecVersionRecord(
        Guid.NewGuid(),
        project.Id,
        Number: 1,
        SpecVersionStatus.Draft,
        BaseVersionId: null,
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(hashInput))).ToLowerInvariant(),
        GenerationProfile: "balanced",
        targets,
        IsSimulated: false,
        SpecBlobName: null,
        SpecHash: null,
        now,
        ApprovedAt: null);
    await store.SaveVersionAsync(version, cancellationToken).ConfigureAwait(false);
    var response = await ApiResponseMapper.ProjectAsync(project, store, cancellationToken).ConfigureAwait(false);
    return Results.Created($"/api/projects/{project.Id}", response);
});

api.MapGet("/projects", async (AjureStore store, CancellationToken cancellationToken) =>
{
    var projects = await store.ListProjectsAsync(cancellationToken).ConfigureAwait(false);
    var responses = new List<ProjectSummaryResponse>(projects.Count);
    foreach (var project in projects)
    {
        var response = await ApiResponseMapper.ProjectAsync(project, store, cancellationToken).ConfigureAwait(false);
        responses.Add(ApiResponseMapper.Summary(response));
    }

    return Results.Ok(responses.OrderByDescending(static project => project.UpdatedAt));
});

api.MapGet("/projects/{projectId:guid}", async (
    Guid projectId,
    HttpContext context,
    AjureStore store,
    CancellationToken cancellationToken) =>
{
    var project = await store.GetProjectAsync(projectId, cancellationToken).ConfigureAwait(false);
    if (project is null)
    {
        return ApiProblems.NotFound(context, "project_not_found", "The project was not found.");
    }

    return Results.Ok(await ApiResponseMapper.ProjectAsync(project, store, cancellationToken).ConfigureAwait(false));
});

api.MapPost("/projects/{projectId:guid}/analyze", async (
    Guid projectId,
    HttpContext context,
    AjureStore store,
    CancellationToken cancellationToken) =>
{
    if (await store.GetProjectAsync(projectId, cancellationToken).ConfigureAwait(false) is null)
    {
        return ApiProblems.NotFound(context, "project_not_found", "The project was not found.");
    }

    return await QueueJobAsync(
        JobKind.Analyze,
        projectId,
        specVersionId: null,
        baseVersionId: null,
        context,
        store,
        cancellationToken).ConfigureAwait(false);
});

api.MapGet("/projects/{projectId:guid}/decisions", async (
    Guid projectId,
    HttpContext context,
    AjureStore store,
    CancellationToken cancellationToken) =>
{
    if (await store.GetProjectAsync(projectId, cancellationToken).ConfigureAwait(false) is null)
    {
        return ApiProblems.NotFound(context, "project_not_found", "The project was not found.");
    }

    var decisions = await store.ListDecisionsAsync(projectId, cancellationToken).ConfigureAwait(false);
    return Results.Ok(decisions.Select(ApiResponseMapper.Decision));
});

api.MapPut("/projects/{projectId:guid}/decisions/{decisionId}", async (
    Guid projectId,
    string decisionId,
    UpdateDecisionRequest request,
    HttpContext context,
    AjureStore store,
    CancellationToken cancellationToken) =>
{
    var decision = await store
        .GetDecisionAsync(projectId, decisionId, cancellationToken)
        .ConfigureAwait(false);
    if (decision is null)
    {
        return ApiProblems.NotFound(context, "decision_not_found", "The decision was not found.");
    }

    var answer = string.IsNullOrWhiteSpace(request.Text)
        ? request.OptionId ?? request.Answer
        : request.Text;
    if (string.IsNullOrWhiteSpace(answer) || answer.Length > 2_000)
    {
        return ApiProblems.Validation(
            context,
            "invalid_decision_answer",
            "Decision answer must contain 1 to 2,000 characters.");
    }

    decision = decision with
    {
        Answer = answer.Trim(),
        AnswerOptionId = request.OptionId,
        AnswerText = string.IsNullOrWhiteSpace(request.Text) ? null : request.Text.Trim(),
        UpdatedAt = DateTimeOffset.UtcNow
    };
    await store.SaveDecisionAsync(decision, cancellationToken).ConfigureAwait(false);
    return Results.Ok(ApiResponseMapper.Decision(decision));
});

api.MapPost("/projects/{projectId:guid}/versions", async (
    Guid projectId,
    CreateVersionRequest request,
    HttpContext context,
    AjureStore store,
    CancellationToken cancellationToken) =>
{
    var project = await store.GetProjectAsync(projectId, cancellationToken).ConfigureAwait(false);
    if (project is null)
    {
        return ApiProblems.NotFound(context, "project_not_found", "The project was not found.");
    }

    if (request.BaseVersionId is { } baseVersionId)
    {
        var baseVersion = await store.GetVersionAsync(baseVersionId, cancellationToken).ConfigureAwait(false);
        if (baseVersion is null)
        {
            return ApiProblems.Validation(
                context,
                "base_version_not_found",
                "The requested base version was not found.");
        }

        if (baseVersion.ProjectId != projectId)
        {
            return ApiProblems.Validation(
                context,
                "base_version_project_mismatch",
                "The base version belongs to another project.");
        }
    }

    var targets = NormalizeTargets(request.TargetIds);
    if (targets is null)
    {
        return ApiProblems.Validation(
            context,
            "invalid_target",
            "One or more target IDs are not supported.");
    }

    var versions = await store.ListVersionsAsync(projectId, cancellationToken).ConfigureAwait(false);
    var number = versions.Count == 0 ? 1 : checked(versions.Max(static version => version.Number) + 1);
    var profile = string.IsNullOrWhiteSpace(request.GenerationProfile)
        ? "balanced"
        : request.GenerationProfile.Trim();
    if (!ApiConstants.SupportedProfiles.Contains(profile))
    {
        return ApiProblems.Validation(
            context,
            "invalid_generation_profile",
            "The generation profile is not supported.");
    }

    var hashInput = $"{project.Idea}\n{profile}\n{string.Join(',', targets)}\n{request.BaseVersionId}";
    var version = new SpecVersionRecord(
        Guid.NewGuid(),
        projectId,
        number,
        SpecVersionStatus.Draft,
        request.BaseVersionId,
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(hashInput))).ToLowerInvariant(),
        profile,
        targets,
        IsSimulated: false,
        SpecBlobName: null,
        SpecHash: null,
        DateTimeOffset.UtcNow,
        ApprovedAt: null);
    await store.SaveVersionAsync(version, cancellationToken).ConfigureAwait(false);
    return Results.Created($"/api/spec-versions/{version.Id}", version);
});

api.MapPost("/spec-versions/{versionId:guid}/generate", async (
    Guid versionId,
    HttpContext context,
    AjureStore store,
    CancellationToken cancellationToken) =>
{
    var version = await store.GetVersionAsync(versionId, cancellationToken).ConfigureAwait(false);
    if (version is null)
    {
        return ApiProblems.NotFound(context, "version_not_found", "The specification version was not found.");
    }

    if (version.Status is not SpecVersionStatus.Draft and not SpecVersionStatus.NeedsDecision)
    {
        return ApiProblems.Conflict(
            context,
            "version_not_generatable",
            "Only Draft or NeedsDecision versions can be generated.");
    }

    return await QueueJobAsync(
        JobKind.Generate,
        version.ProjectId,
        version.Id,
        version.BaseVersionId,
        context,
        store,
        cancellationToken).ConfigureAwait(false);
});

api.MapGet("/jobs/{jobId:guid}", async (
    Guid jobId,
    HttpContext context,
    AjureStore store,
    CancellationToken cancellationToken) =>
{
    var job = await store.GetJobAsync(jobId, cancellationToken).ConfigureAwait(false);
    if (job is null)
    {
        return ApiProblems.NotFound(context, "job_not_found", "The job was not found.");
    }

    var events = await store.ListEventsAsync(jobId, 0, cancellationToken).ConfigureAwait(false);
    return Results.Ok(ApiResponseMapper.Job(job, events));
});

api.MapGet("/jobs/{jobId:guid}/events", SseEndpoint.StreamAsync);

api.MapGet("/spec-versions/{versionId:guid}/artifacts", async (
    Guid versionId,
    HttpContext context,
    AjureStore store,
    CancellationToken cancellationToken) =>
{
    if (await store.GetVersionAsync(versionId, cancellationToken).ConfigureAwait(false) is null)
    {
        return ApiProblems.NotFound(context, "version_not_found", "The specification version was not found.");
    }

    var version = await store.GetVersionAsync(versionId, cancellationToken).ConfigureAwait(false);
    if (version is null)
    {
        return ApiProblems.NotFound(context, "version_not_found", "The specification version was not found.");
    }

    var artifacts = await store.ListArtifactsAsync(versionId, cancellationToken).ConfigureAwait(false);
    return Results.Ok(
        artifacts
            .Where(static artifact =>
                artifact.Status != ArtifactStatus.Proposed
                && artifact.Kind is not ArtifactKind.ExportZip
                && artifact.Kind is not ArtifactKind.ValidationReport)
            .Select(artifact => ApiResponseMapper.Artifact(artifact, version.Number)));
});

api.MapGet("/artifacts/{artifactId:guid}", async (
    Guid artifactId,
    HttpContext context,
    AjureStore store,
    CancellationToken cancellationToken) =>
{
    var artifact = await store.GetArtifactAsync(artifactId, cancellationToken).ConfigureAwait(false);
    if (artifact is null)
    {
        return ApiProblems.NotFound(context, "artifact_not_found", "The artifact was not found.");
    }

    var content = await store.GetBlobAsync(artifact.BlobName, cancellationToken).ConfigureAwait(false);
    if (content is null)
    {
        return ApiProblems.NotFound(context, "artifact_content_not_found", "The artifact content was not found.");
    }

    if (artifact.Kind == ArtifactKind.ExportZip)
    {
        return Results.File(content.ToArray(), artifact.ContentType, Path.GetFileName(artifact.Path));
    }

    var version = await store.GetVersionAsync(artifact.SpecVersionId, cancellationToken).ConfigureAwait(false)
        ?? throw new InvalidDataException("The artifact version was not found.");
    var mapped = ApiResponseMapper.Artifact(artifact, version.Number);
    return Results.Ok(new ArtifactContentResponse(
        mapped.Id,
        mapped.Kind,
        mapped.TargetId,
        mapped.Path,
        mapped.Status,
        mapped.SpecVersionNumber,
        mapped.ContentHash,
        mapped.UpdatedAt,
        mapped.StaleReason,
        content.ToString()));
});

api.MapPut("/artifacts/{artifactId:guid}", async (
    Guid artifactId,
    UpdateArtifactRequest request,
    HttpContext context,
    AjureStore store,
    CancellationToken cancellationToken) =>
{
    var artifact = await store.GetArtifactAsync(artifactId, cancellationToken).ConfigureAwait(false);
    if (artifact is null)
    {
        return ApiProblems.NotFound(context, "artifact_not_found", "The artifact was not found.");
    }

    if (artifact.ContentType != "text/markdown")
    {
        return ApiProblems.Conflict(
            context,
            "artifact_not_editable",
            "Only Markdown artifacts accept edit proposals.");
    }

    if (string.IsNullOrWhiteSpace(request.Content) || request.Content.Length > 200_000)
    {
        return ApiProblems.Validation(
            context,
            "invalid_artifact_content",
            "Artifact content must contain 1 to 200,000 characters.");
    }

    var blobName = $"proposals/{artifact.SpecVersionId:N}/{Guid.NewGuid():N}.md";
    var content = BinaryData.FromString(request.Content);
    await store.PutBlobAsync(blobName, content, "text/markdown", cancellationToken).ConfigureAwait(false);
    var updated = artifact with
    {
        Status = ArtifactStatus.Current,
        BlobName = blobName,
        ContentHash = Convert
            .ToHexString(SHA256.HashData(content.ToArray()))
            .ToLowerInvariant(),
        CreatedAt = DateTimeOffset.UtcNow
    };
    await store.SaveArtifactAsync(updated, cancellationToken).ConfigureAwait(false);
    var artifacts = await store.ListArtifactsAsync(artifact.SpecVersionId, cancellationToken).ConfigureAwait(false);
    var affected = artifacts
        .Where(item =>
            item.Id != artifact.Id
            && item.Status == ArtifactStatus.Current
            && item.Kind is not ArtifactKind.ExportZip)
        .ToArray();
    foreach (var stale in affected)
    {
        await store.SaveArtifactAsync(stale with { Status = ArtifactStatus.Stale }, cancellationToken)
            .ConfigureAwait(false);
    }

    var version = await store.GetVersionAsync(artifact.SpecVersionId, cancellationToken).ConfigureAwait(false)
        ?? throw new InvalidDataException("The artifact version was not found.");
    await store.SaveVersionAsync(
            version with { Status = SpecVersionStatus.Draft, ApprovedAt = null },
            cancellationToken)
        .ConfigureAwait(false);
    return Results.Ok(new ArtifactSaveResponse(
        ApiResponseMapper.Artifact(updated, version.Number),
        affected.Select(static item => item.Path).ToArray(),
        "Draft"));
});

api.MapPost("/spec-versions/{versionId:guid}/validate", async (
    Guid versionId,
    HttpContext context,
    AjureStore store,
    CancellationToken cancellationToken) =>
{
    var version = await store.GetVersionAsync(versionId, cancellationToken).ConfigureAwait(false);
    if (version is null)
    {
        return ApiProblems.NotFound(context, "version_not_found", "The specification version was not found.");
    }

    return await QueueJobAsync(
        JobKind.Validate,
        version.ProjectId,
        version.Id,
        version.BaseVersionId,
        context,
        store,
        cancellationToken).ConfigureAwait(false);
});

api.MapGet("/validation-runs/{runId:guid}", async (
    Guid runId,
    HttpContext context,
    AjureStore store,
    CancellationToken cancellationToken) =>
{
    var run = await store.GetValidationRunAsync(runId, cancellationToken).ConfigureAwait(false);
    return run is null
        ? ApiProblems.NotFound(context, "validation_run_not_found", "The validation run was not found.")
        : Results.Ok(await ApiResponseMapper.ValidationRunAsync(run, store, cancellationToken).ConfigureAwait(false));
});

api.MapGet("/spec-versions/{versionId:guid}/diff/{baseId:guid}", async (
    Guid versionId,
    Guid baseId,
    HttpContext context,
    AjureStore store,
    CancellationToken cancellationToken) =>
{
    var version = await store.GetVersionAsync(versionId, cancellationToken).ConfigureAwait(false);
    var baseline = await store.GetVersionAsync(baseId, cancellationToken).ConfigureAwait(false);
    if (version is null || baseline is null)
    {
        return ApiProblems.NotFound(context, "version_not_found", "A requested specification version was not found.");
    }

    if (version.ProjectId != baseline.ProjectId)
    {
        return ApiProblems.Validation(
            context,
            "version_project_mismatch",
            "The compared versions belong to different projects.");
    }

    if (string.IsNullOrWhiteSpace(version.SpecBlobName)
        || string.IsNullOrWhiteSpace(baseline.SpecBlobName))
    {
        return ApiProblems.Conflict(
            context,
            "spec_content_missing",
            "A compared version does not have persisted ProjectSpec content.");
    }

    var versionContent = await store.GetBlobAsync(version.SpecBlobName, cancellationToken).ConfigureAwait(false);
    var baselineContent = await store.GetBlobAsync(baseline.SpecBlobName, cancellationToken).ConfigureAwait(false);
    if (versionContent is null || baselineContent is null)
    {
        return ApiProblems.Conflict(
            context,
            "spec_content_missing",
            "A compared ProjectSpec blob was not found.");
    }

    var candidateGraph = RequirementGraph.Build(SpecJson.Deserialize<ProjectSpec>(versionContent.ToString()));
    var baselineGraph = RequirementGraph.Build(SpecJson.Deserialize<ProjectSpec>(baselineContent.ToString()));
    var candidateIds = candidateGraph.Nodes.Select(static node => node.Id).ToHashSet(StringComparer.Ordinal);
    var baselineIds = baselineGraph.Nodes.Select(static node => node.Id).ToHashSet(StringComparer.Ordinal);
    var added = candidateIds.Except(baselineIds, StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
    var removed = baselineIds.Except(candidateIds, StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
    var changed = candidateIds
        .Intersect(baselineIds, StringComparer.Ordinal)
        .Where(id => !string.Equals(
            CanonicalJson.Serialize(candidateGraph.Find(id)),
            CanonicalJson.Serialize(baselineGraph.Find(id)),
            StringComparison.Ordinal))
        .Order(StringComparer.Ordinal)
        .ToList();
    if (added.Length == 0
        && removed.Length == 0
        && changed.Count == 0
        && !string.Equals(version.SpecHash, baseline.SpecHash, StringComparison.Ordinal))
    {
        changed.Add("ProjectSpec");
    }

    return Results.Ok(new SemanticDiff(
        versionId,
        baseId,
        added,
        removed,
        changed.ToArray()));
});

api.MapPost("/spec-versions/{versionId:guid}/export", async (
    Guid versionId,
    HttpContext context,
    AjureStore store,
    CancellationToken cancellationToken) =>
{
    var version = await store.GetVersionAsync(versionId, cancellationToken).ConfigureAwait(false);
    if (version is null)
    {
        return ApiProblems.NotFound(context, "version_not_found", "The specification version was not found.");
    }

    if (version.Status != SpecVersionStatus.Ready)
    {
        return ApiProblems.Conflict(
            context,
            "version_not_ready",
            "Only a Ready specification version can be exported.");
    }

    var job = await EnqueueJobAsync(
            JobKind.Export,
            version.ProjectId,
            version.Id,
            version.BaseVersionId,
            context,
            store,
            cancellationToken)
        .ConfigureAwait(false);
    if (!context.Request.Headers.Accept.Any(static value =>
            value?.Contains("application/zip", StringComparison.OrdinalIgnoreCase) == true))
    {
        return Results.Accepted($"/api/jobs/{job.Id}", new JobAcceptedResponse(job.Id));
    }

    var completed = await WaitForJobAsync(job.Id, store, cancellationToken).ConfigureAwait(false);
    if (completed.Status != JobStatus.Succeeded || completed.OutputArtifactId is not { } artifactId)
    {
        return ApiProblems.Conflict(
            context,
            completed.ErrorCode ?? "export_failed",
            completed.ErrorMessage ?? "The ZIP export did not complete.");
    }

    var artifact = await store.GetArtifactAsync(artifactId, cancellationToken).ConfigureAwait(false)
        ?? throw new InvalidDataException("The export artifact was not found.");
    var content = await store.GetBlobAsync(artifact.BlobName, cancellationToken).ConfigureAwait(false)
        ?? throw new InvalidDataException("The export content was not found.");
    return Results.File(content.ToArray(), artifact.ContentType, Path.GetFileName(artifact.Path));
});

app.Run();

static async Task<IResult> QueueJobAsync(
    JobKind kind,
    Guid projectId,
    Guid? specVersionId,
    Guid? baseVersionId,
    HttpContext context,
    AjureStore store,
    CancellationToken cancellationToken)
{
    var job = await EnqueueJobAsync(
            kind,
            projectId,
            specVersionId,
            baseVersionId,
            context,
            store,
            cancellationToken)
        .ConfigureAwait(false);
    return Results.Accepted($"/api/jobs/{job.Id}", new JobAcceptedResponse(job.Id));
}

static async Task<JobRecord> EnqueueJobAsync(
    JobKind kind,
    Guid projectId,
    Guid? specVersionId,
    Guid? baseVersionId,
    HttpContext context,
    AjureStore store,
    CancellationToken cancellationToken)
{
    var job = new JobRecord(
        Guid.NewGuid(),
        kind,
        projectId,
        specVersionId,
        baseVersionId,
        JobStatus.Queued,
        LastSequence: 0,
        IsSimulated: false,
        ValidationRunId: null,
        OutputArtifactId: null,
        ErrorCode: null,
        ErrorMessage: null,
        context.TraceIdentifier,
        DateTimeOffset.UtcNow,
        StartedAt: null,
        CompletedAt: null);
    await store.SaveJobAsync(job, cancellationToken).ConfigureAwait(false);
    await store.AppendEventAsync(
            job.Id,
            "job.queued",
            "queue",
            "queued",
            $"{kind} job queued.",
            retryable: false,
            cancellationToken)
        .ConfigureAwait(false);
    await store
        .EnqueueAsync(new JobMessage(job.Id, kind, projectId, specVersionId, baseVersionId), cancellationToken)
        .ConfigureAwait(false);
    return job;
}

static async Task<JobRecord> WaitForJobAsync(
    Guid jobId,
    AjureStore store,
    CancellationToken cancellationToken)
{
    var deadline = DateTimeOffset.UtcNow + TimeSpan.FromMinutes(5);
    while (DateTimeOffset.UtcNow < deadline)
    {
        var job = await store.GetJobAsync(jobId, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidDataException("The export job was not found.");
        if (job.Status is JobStatus.Succeeded or JobStatus.Failed)
        {
            return job;
        }

        await Task.Delay(TimeSpan.FromMilliseconds(250), cancellationToken).ConfigureAwait(false);
    }

    throw new TimeoutException("The export job did not complete within five minutes.");
}

static string[]? NormalizeTargets(string[]? requestedTargets)
{
    var targets = requestedTargets is { Length: > 0 }
        ? requestedTargets
            .Where(static target => !string.IsNullOrWhiteSpace(target))
            .Select(static target => target.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToArray()
        : ["github-copilot"];
    return targets.All(ApiConstants.SupportedTargets.Contains) ? targets : null;
}

public partial class Program;

internal static class ApiConstants
{
    internal static readonly string[] ProjectSpecChange = ["ProjectSpec"];

    internal static readonly HashSet<string> SupportedProfiles = new(StringComparer.Ordinal)
    {
        "balanced",
        "speed",
        "strict"
    };

    internal static readonly HashSet<string> SupportedTargets = new(StringComparer.Ordinal)
    {
        "claude-code",
        "github-copilot",
        "openai-codex",
        "gemini-cli",
        "cursor",
        "devin-windsurf",
        "cline",
        "amazon-q",
        "generic"
    };
}
