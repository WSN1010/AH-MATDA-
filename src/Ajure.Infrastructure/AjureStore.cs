using System.Text.Json;
using Azure;
using Azure.Data.Tables;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Queues;
using Azure.Storage.Queues.Models;

namespace Ajure.Infrastructure;

public sealed class AjureStore
{
    private const string ProjectsPartition = "projects";
    private const string VersionsPartition = "versions";
    private const string JobsPartition = "jobs";
    private const string RunsPartition = "runs";
    private readonly TableClient _projects;
    private readonly TableClient _versions;
    private readonly TableClient _decisions;
    private readonly TableClient _jobs;
    private readonly TableClient _events;
    private readonly TableClient _artifacts;
    private readonly TableClient _runs;
    private readonly BlobContainerClient _content;
    private readonly QueueClient _queue;
    private readonly QueueClient _poisonQueue;

    public AjureStore(
        TableServiceClient tableService,
        BlobServiceClient blobService,
        QueueServiceClient queueService)
    {
        ArgumentNullException.ThrowIfNull(tableService);
        ArgumentNullException.ThrowIfNull(blobService);
        ArgumentNullException.ThrowIfNull(queueService);

        _projects = tableService.GetTableClient("AjureProjects");
        _versions = tableService.GetTableClient("AjureVersions");
        _decisions = tableService.GetTableClient("AjureDecisions");
        _jobs = tableService.GetTableClient("AjureJobs");
        _events = tableService.GetTableClient("AjureJobEvents");
        _artifacts = tableService.GetTableClient("AjureArtifacts");
        _runs = tableService.GetTableClient("AjureValidationRuns");

        _content = blobService.GetBlobContainerClient("ajure-content");

        _queue = queueService.GetQueueClient("ajure-jobs");
        _poisonQueue = queueService.GetQueueClient("ajure-jobs-poison");
    }

    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        await Task.WhenAll(
            _projects.CreateIfNotExistsAsync(cancellationToken),
            _versions.CreateIfNotExistsAsync(cancellationToken),
            _decisions.CreateIfNotExistsAsync(cancellationToken),
            _jobs.CreateIfNotExistsAsync(cancellationToken),
            _events.CreateIfNotExistsAsync(cancellationToken),
            _artifacts.CreateIfNotExistsAsync(cancellationToken),
            _runs.CreateIfNotExistsAsync(cancellationToken),
            _content.CreateIfNotExistsAsync(PublicAccessType.None, cancellationToken: cancellationToken),
            _queue.CreateIfNotExistsAsync(cancellationToken: cancellationToken),
            _poisonQueue.CreateIfNotExistsAsync(cancellationToken: cancellationToken)).ConfigureAwait(false);
    }

    public Task CreateProjectAsync(ProjectRecord project, CancellationToken cancellationToken) =>
        _projects.AddEntityAsync(ToEntity(ProjectsPartition, Key(project.Id), project), cancellationToken);

    public async Task<IReadOnlyList<ProjectRecord>> ListProjectsAsync(CancellationToken cancellationToken)
    {
        var projects = new List<ProjectRecord>();
        await foreach (var entity in _projects.QueryAsync<TableEntity>(
                           static entity => entity.PartitionKey == ProjectsPartition,
                           cancellationToken: cancellationToken))
        {
            projects.Add(FromEntity<ProjectRecord>(entity));
        }

        return projects.OrderByDescending(static project => project.CreatedAt).ToArray();
    }

    public Task<ProjectRecord?> GetProjectAsync(Guid projectId, CancellationToken cancellationToken) =>
        GetAsync<ProjectRecord>(_projects, ProjectsPartition, Key(projectId), cancellationToken);

    public Task SaveVersionAsync(SpecVersionRecord version, CancellationToken cancellationToken) =>
        _versions.UpsertEntityAsync(
            ToEntity(VersionsPartition, Key(version.Id), version, ("ProjectId", Key(version.ProjectId))),
            TableUpdateMode.Replace,
            cancellationToken);

    public Task<SpecVersionRecord?> GetVersionAsync(Guid versionId, CancellationToken cancellationToken) =>
        GetAsync<SpecVersionRecord>(_versions, VersionsPartition, Key(versionId), cancellationToken);

    public async Task<IReadOnlyList<SpecVersionRecord>> ListVersionsAsync(
        Guid projectId,
        CancellationToken cancellationToken)
    {
        var filter = TableClient.CreateQueryFilter($"PartitionKey eq {VersionsPartition} and ProjectId eq {Key(projectId)}");
        var versions = new List<SpecVersionRecord>();
        await foreach (var entity in _versions.QueryAsync<TableEntity>(filter, cancellationToken: cancellationToken))
        {
            versions.Add(FromEntity<SpecVersionRecord>(entity));
        }

        return versions.OrderByDescending(static version => version.Number).ToArray();
    }

    public Task SaveDecisionAsync(DecisionRecord decision, CancellationToken cancellationToken) =>
        _decisions.UpsertEntityAsync(
            ToEntity(Key(decision.ProjectId), decision.Id, decision),
            TableUpdateMode.Replace,
            cancellationToken);

    public Task<DecisionRecord?> GetDecisionAsync(
        Guid projectId,
        string decisionId,
        CancellationToken cancellationToken) =>
        GetAsync<DecisionRecord>(_decisions, Key(projectId), decisionId, cancellationToken);

    public async Task<IReadOnlyList<DecisionRecord>> ListDecisionsAsync(
        Guid projectId,
        CancellationToken cancellationToken)
    {
        var decisions = new List<DecisionRecord>();
        await foreach (var entity in _decisions.QueryAsync<TableEntity>(
                           entity => entity.PartitionKey == Key(projectId),
                           cancellationToken: cancellationToken))
        {
            decisions.Add(FromEntity<DecisionRecord>(entity));
        }

        return decisions.OrderBy(static decision => decision.Id, StringComparer.Ordinal).ToArray();
    }

    public Task SaveJobAsync(JobRecord job, CancellationToken cancellationToken) =>
        _jobs.UpsertEntityAsync(
            ToEntity(JobsPartition, Key(job.Id), job),
            TableUpdateMode.Replace,
            cancellationToken);

    public Task<JobRecord?> GetJobAsync(Guid jobId, CancellationToken cancellationToken) =>
        GetAsync<JobRecord>(_jobs, JobsPartition, Key(jobId), cancellationToken);

    public async Task<IReadOnlyList<JobRecord>> ListJobsAsync(
        Guid projectId,
        CancellationToken cancellationToken)
    {
        // ponytail: MVP job volume is small; add a ProjectId table index if this scan becomes measurable.
        var jobs = new List<JobRecord>();
        await foreach (var entity in _jobs.QueryAsync<TableEntity>(
                           static entity => entity.PartitionKey == JobsPartition,
                           cancellationToken: cancellationToken))
        {
            var job = FromEntity<JobRecord>(entity);
            if (job.ProjectId == projectId)
            {
                jobs.Add(job);
            }
        }

        return jobs.OrderByDescending(static job => job.CreatedAt).ToArray();
    }

    public async Task EnqueueAsync(JobMessage message, CancellationToken cancellationToken)
    {
        var payload = BinaryData.FromObjectAsJson(message, JsonDefaults.Options);
        await _queue.SendMessageAsync(payload.ToString(), cancellationToken).ConfigureAwait(false);
    }

    public async Task<DequeuedJob?> DequeueAsync(CancellationToken cancellationToken)
    {
        var response = await _queue
            .ReceiveMessageAsync(TimeSpan.FromMinutes(30), cancellationToken)
            .ConfigureAwait(false);
        var message = response.Value;
        if (message is null)
        {
            return null;
        }

        var job = message.Body.ToObjectFromJson<JobMessage>(JsonDefaults.Options)
            ?? throw new InvalidDataException("Queue message did not contain a job.");
        return new DequeuedJob(job, message.MessageId, message.PopReceipt, message.DequeueCount);
    }

    public Task DeleteMessageAsync(DequeuedJob job, CancellationToken cancellationToken) =>
        _queue.DeleteMessageAsync(job.MessageId, job.PopReceipt, cancellationToken);

    public Task ReleaseForRetryAsync(
        DequeuedJob job,
        TimeSpan delay,
        CancellationToken cancellationToken)
    {
        var payload = BinaryData.FromObjectAsJson(job.Message, JsonDefaults.Options);
        return _queue.UpdateMessageAsync(
            job.MessageId,
            job.PopReceipt,
            payload.ToString(),
            delay,
            cancellationToken);
    }

    public async Task MoveToPoisonAsync(
        DequeuedJob job,
        string errorType,
        CancellationToken cancellationToken)
    {
        var payload = BinaryData.FromObjectAsJson(
            new PoisonJobMessage(job.Message, errorType, DateTimeOffset.UtcNow),
            JsonDefaults.Options);
        await _poisonQueue.SendMessageAsync(payload.ToString(), cancellationToken).ConfigureAwait(false);
        await DeleteMessageAsync(job, cancellationToken).ConfigureAwait(false);
    }

    public async Task<JobEventRecord> AppendEventAsync(
        Guid jobId,
        string eventType,
        string stage,
        string status,
        string summary,
        bool retryable,
        CancellationToken cancellationToken)
    {
        var job = await GetJobAsync(jobId, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Job '{jobId}' was not found.");
        // ponytail: event streams are small; add a lease-backed counter if per-Job volume grows.
        var persistedEvents = await ListEventsAsync(jobId, 0, cancellationToken).ConfigureAwait(false);
        var persistedSequence = persistedEvents.Count == 0 ? 0 : persistedEvents[^1].Sequence;
        var sequence = checked(Math.Max(job.LastSequence, persistedSequence) + 1);
        var jobEvent = new JobEventRecord(
            jobId,
            sequence,
            eventType,
            stage,
            status,
            summary,
            DateTimeOffset.UtcNow,
            retryable,
            job.CorrelationId);
        await _events
            .AddEntityAsync(ToEntity(Key(jobId), sequence.ToString("D20", System.Globalization.CultureInfo.InvariantCulture), jobEvent), cancellationToken)
            .ConfigureAwait(false);
        await SaveJobAsync(job with { LastSequence = sequence }, cancellationToken).ConfigureAwait(false);
        return jobEvent;
    }

    public async Task<IReadOnlyList<JobEventRecord>> ListEventsAsync(
        Guid jobId,
        long afterSequence,
        CancellationToken cancellationToken)
    {
        var events = new List<JobEventRecord>();
        await foreach (var entity in _events.QueryAsync<TableEntity>(
                           entity => entity.PartitionKey == Key(jobId),
                           cancellationToken: cancellationToken))
        {
            var jobEvent = FromEntity<JobEventRecord>(entity);
            if (jobEvent.Sequence > afterSequence)
            {
                events.Add(jobEvent);
            }
        }

        return events.OrderBy(static item => item.Sequence).ToArray();
    }

    public Task SaveArtifactAsync(ArtifactRecord artifact, CancellationToken cancellationToken) =>
        _artifacts.UpsertEntityAsync(
            ToEntity(
                Key(artifact.SpecVersionId),
                Key(artifact.Id),
                artifact,
                ("ArtifactId", Key(artifact.Id))),
            TableUpdateMode.Replace,
            cancellationToken);

    public async Task<ArtifactRecord?> GetArtifactAsync(
        Guid artifactId,
        CancellationToken cancellationToken)
    {
        var filter = TableClient.CreateQueryFilter($"ArtifactId eq {Key(artifactId)}");
        await foreach (var entity in _artifacts.QueryAsync<TableEntity>(
                           filter,
                           maxPerPage: 1,
                           cancellationToken: cancellationToken))
        {
            return FromEntity<ArtifactRecord>(entity);
        }

        return null;
    }

    public async Task<IReadOnlyList<ArtifactRecord>> ListArtifactsAsync(
        Guid versionId,
        CancellationToken cancellationToken)
    {
        var artifacts = new List<ArtifactRecord>();
        await foreach (var entity in _artifacts.QueryAsync<TableEntity>(
                           entity => entity.PartitionKey == Key(versionId),
                           cancellationToken: cancellationToken))
        {
            artifacts.Add(FromEntity<ArtifactRecord>(entity));
        }

        return artifacts.OrderBy(static artifact => artifact.Path, StringComparer.Ordinal).ToArray();
    }

    public async Task PutBlobAsync(
        string blobName,
        BinaryData content,
        string contentType,
        CancellationToken cancellationToken)
    {
        var blob = _content.GetBlobClient(blobName);
        await blob.UploadAsync(
                content,
                new BlobUploadOptions
                {
                    HttpHeaders = new BlobHttpHeaders { ContentType = contentType }
                },
                cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<BinaryData?> GetBlobAsync(string blobName, CancellationToken cancellationToken)
    {
        var blob = _content.GetBlobClient(blobName);
        try
        {
            var response = await blob.DownloadContentAsync(cancellationToken).ConfigureAwait(false);
            return response.Value.Content;
        }
        catch (RequestFailedException exception) when (exception.Status == 404)
        {
            return null;
        }
    }

    public Task SaveValidationRunAsync(
        ValidationRunRecord run,
        CancellationToken cancellationToken) =>
        _runs.UpsertEntityAsync(
            ToEntity(RunsPartition, Key(run.Id), run, ("VersionId", Key(run.SpecVersionId))),
            TableUpdateMode.Replace,
            cancellationToken);

    public Task<ValidationRunRecord?> GetValidationRunAsync(
        Guid runId,
        CancellationToken cancellationToken) =>
        GetAsync<ValidationRunRecord>(_runs, RunsPartition, Key(runId), cancellationToken);

    public async Task<ValidationRunRecord?> GetLatestValidationRunAsync(
        Guid versionId,
        CancellationToken cancellationToken)
    {
        var filter = TableClient.CreateQueryFilter($"PartitionKey eq {RunsPartition} and VersionId eq {Key(versionId)}");
        ValidationRunRecord? latest = null;
        await foreach (var entity in _runs.QueryAsync<TableEntity>(filter, cancellationToken: cancellationToken))
        {
            var run = FromEntity<ValidationRunRecord>(entity);
            if (latest is null || run.StartedAt > latest.StartedAt)
            {
                latest = run;
            }
        }

        return latest;
    }

    private static async Task<T?> GetAsync<T>(
        TableClient table,
        string partitionKey,
        string rowKey,
        CancellationToken cancellationToken)
    {
        try
        {
            var response = await table
                .GetEntityAsync<TableEntity>(partitionKey, rowKey, cancellationToken: cancellationToken)
                .ConfigureAwait(false);
            return FromEntity<T>(response.Value);
        }
        catch (RequestFailedException exception) when (exception.Status == 404)
        {
            return default;
        }
    }

    private static TableEntity ToEntity(
        string partitionKey,
        string rowKey,
        object value,
        params (string Name, string Value)[] indexedValues)
    {
        var entity = new TableEntity(partitionKey, rowKey)
        {
            ["Data"] = JsonSerializer.Serialize(value, value.GetType(), JsonDefaults.Options)
        };

        foreach (var indexedValue in indexedValues)
        {
            entity[indexedValue.Name] = indexedValue.Value;
        }

        return entity;
    }

    private static T FromEntity<T>(TableEntity entity)
    {
        var json = entity.GetString("Data")
            ?? throw new InvalidDataException("Stored entity did not contain data.");
        return JsonSerializer.Deserialize<T>(json, JsonDefaults.Options)
            ?? throw new InvalidDataException($"Stored entity could not be read as {typeof(T).Name}.");
    }

    private static string Key(Guid id) => id.ToString("N");
}

public static class JsonDefaults
{
    public static JsonSerializerOptions Options { get; } = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = false
    };
}
