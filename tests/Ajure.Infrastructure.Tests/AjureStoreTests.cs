using Microsoft.Data.Sqlite;

namespace Ajure.Infrastructure.Tests;

public sealed class AjureStoreTests : IAsyncLifetime
{
    private readonly string _dataPath = Path.Combine(
        AppContext.BaseDirectory,
        "storage-test-data",
        $"{Guid.NewGuid():N}.db");
    private AjureStore _store = null!;

    public async Task InitializeAsync()
    {
        _store = new AjureStore(new StorageOptions
        {
            DataPath = _dataPath,
            BusyTimeoutMilliseconds = 5_000,
            LeaseSeconds = 60
        });
        await _store.InitializeAsync(CancellationToken.None);
    }

    public Task DisposeAsync()
    {
        SqliteConnection.ClearAllPools();
        foreach (var path in new[] { _dataPath, _dataPath + "-shm", _dataPath + "-wal" })
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }

        var directory = Path.GetDirectoryName(_dataPath);
        if (directory is not null && Directory.Exists(directory) && !Directory.EnumerateFileSystemEntries(directory).Any())
        {
            Directory.Delete(directory);
        }

        return Task.CompletedTask;
    }

    [Fact]
    public void RejectsRelativeDataPath()
    {
        var options = new StorageOptions { DataPath = "relative.db" };

        var exception = Assert.Throws<ArgumentException>(() => new AjureStore(options));

        Assert.Contains("absolute path", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task PersistsOrderedMetadataAndBlobs()
    {
        var projectId = Guid.NewGuid();
        var first = Project(projectId, "first", DateTimeOffset.UtcNow.AddMinutes(-1));
        var second = Project(Guid.NewGuid(), "second", DateTimeOffset.UtcNow);
        await _store.CreateProjectAsync(first, CancellationToken.None);
        await _store.CreateProjectAsync(second, CancellationToken.None);

        Assert.Equal([second.Id, first.Id], (await _store.ListProjectsAsync(CancellationToken.None)).Select(project => project.Id));
        Assert.Equal(first, await _store.GetProjectAsync(first.Id, CancellationToken.None));

        var firstVersion = Version(Guid.NewGuid(), projectId, 1);
        var secondVersion = Version(Guid.NewGuid(), projectId, 2);
        await _store.SaveVersionAsync(firstVersion, CancellationToken.None);
        await _store.SaveVersionAsync(secondVersion, CancellationToken.None);
        Assert.Equal([2, 1], (await _store.ListVersionsAsync(projectId, CancellationToken.None)).Select(version => version.Number));

        await _store.SaveDecisionAsync(Decision(projectId, "decision-z"), CancellationToken.None);
        await _store.SaveDecisionAsync(Decision(projectId, "decision-a"), CancellationToken.None);
        Assert.Equal(
            ["decision-a", "decision-z"],
            (await _store.ListDecisionsAsync(projectId, CancellationToken.None)).Select(decision => decision.Id));

        await _store.SaveArtifactAsync(Artifact(Guid.NewGuid(), firstVersion.Id, "z.md"), CancellationToken.None);
        await _store.SaveArtifactAsync(Artifact(Guid.NewGuid(), firstVersion.Id, "a.md"), CancellationToken.None);
        Assert.Equal(
            ["a.md", "z.md"],
            (await _store.ListArtifactsAsync(firstVersion.Id, CancellationToken.None)).Select(artifact => artifact.Path));

        var olderRun = ValidationRun(Guid.NewGuid(), firstVersion.Id, DateTimeOffset.UtcNow.AddMinutes(-1));
        var newerRun = ValidationRun(Guid.NewGuid(), firstVersion.Id, DateTimeOffset.UtcNow);
        await _store.SaveValidationRunAsync(olderRun, CancellationToken.None);
        await _store.SaveValidationRunAsync(newerRun, CancellationToken.None);
        Assert.Equal(
            newerRun.Id,
            (await _store.GetLatestValidationRunAsync(firstVersion.Id, CancellationToken.None))?.Id);

        await _store.PutBlobAsync("artifact/a.md", BinaryData.FromString("한글 blob"), "text/markdown", CancellationToken.None);
        Assert.Equal("한글 blob", (await _store.GetBlobAsync("artifact/a.md", CancellationToken.None))?.ToString());
    }

    [Fact]
    public async Task LeasesRetriesPoisonsAndSequencesEventsAtomically()
    {
        var projectId = Guid.NewGuid();
        var job = Job(Guid.NewGuid(), projectId);
        await _store.SaveJobAsync(job, CancellationToken.None);

        var firstMessage = new JobMessage(job.Id, job.Kind, projectId, null, null);
        var secondMessage = firstMessage with { JobId = Guid.NewGuid() };
        await _store.EnqueueAsync(firstMessage, CancellationToken.None);
        await Task.Delay(10);
        await _store.EnqueueAsync(secondMessage, CancellationToken.None);

        var firstLease = Assert.IsType<DequeuedJob>(await _store.DequeueAsync(CancellationToken.None));
        Assert.Equal(firstMessage, firstLease.Message);
        Assert.Equal(1, firstLease.DequeueCount);
        await _store.ReleaseForRetryAsync(firstLease, TimeSpan.Zero, CancellationToken.None);

        var poisonLease = Assert.IsType<DequeuedJob>(await _store.DequeueAsync(CancellationToken.None));
        Assert.Equal(secondMessage, poisonLease.Message);
        await _store.MoveToPoisonAsync(poisonLease, "network_failure", CancellationToken.None);

        var retryLease = Assert.IsType<DequeuedJob>(await _store.DequeueAsync(CancellationToken.None));
        Assert.Equal(firstMessage, retryLease.Message);
        Assert.Equal(2, retryLease.DequeueCount);
        await _store.DeleteMessageAsync(retryLease, CancellationToken.None);
        Assert.Null(await _store.DequeueAsync(CancellationToken.None));

        var events = await Task.WhenAll(
            Enumerable.Range(0, 8).Select(index =>
                _store.AppendEventAsync(
                    job.Id,
                    "job.progress",
                    "worker",
                    "running",
                    $"step {index}",
                    retryable: false,
                    CancellationToken.None)));

        var expectedSequences = Enumerable.Range(1, 8).Select(static value => (long)value);
        Assert.Equal(expectedSequences, events.Select(item => item.Sequence).Order());
        Assert.Equal(
            expectedSequences,
            (await _store.ListEventsAsync(job.Id, 0, CancellationToken.None)).Select(item => item.Sequence));
        Assert.Equal(8L, (await _store.GetJobAsync(job.Id, CancellationToken.None))?.LastSequence);
    }

    private static ProjectRecord Project(Guid id, string name, DateTimeOffset createdAt) =>
        new(id, name, "owner", "ko-KR", "idea", createdAt);

    private static SpecVersionRecord Version(Guid id, Guid projectId, int number) =>
        new(
            id,
            projectId,
            number,
            SpecVersionStatus.Draft,
            null,
            "hash",
            "default",
            ["claude"],
            false,
            null,
            null,
            DateTimeOffset.UtcNow,
            null);

    private static DecisionRecord Decision(Guid projectId, string id) =>
        new(projectId, id, "question", ["yes"], "yes", null, false, DateTimeOffset.UtcNow);

    private static ArtifactRecord Artifact(Guid id, Guid versionId, string path) =>
        new(
            id,
            versionId,
            ArtifactKind.Ideation,
            null,
            path,
            "hash",
            "v1",
            ArtifactStatus.Current,
            $"artifact/{path}",
            "text/markdown",
            DateTimeOffset.UtcNow);

    private static ValidationRunRecord ValidationRun(Guid id, Guid versionId, DateTimeOffset startedAt) =>
        new(id, versionId, null, 1, "completed", 95, [], "[]", ["openai:model"], false, startedAt, startedAt);

    private static JobRecord Job(Guid id, Guid projectId) =>
        new(
            id,
            JobKind.Generate,
            projectId,
            null,
            null,
            JobStatus.Queued,
            0,
            false,
            null,
            null,
            null,
            null,
            "correlation",
            DateTimeOffset.UtcNow,
            null,
            null);
}
