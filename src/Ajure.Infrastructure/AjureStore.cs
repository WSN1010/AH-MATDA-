using System.Globalization;
using System.Text.Json;
using Microsoft.Data.Sqlite;

namespace Ajure.Infrastructure;

public sealed class AjureStore
{
    private readonly string _connectionString;
    private readonly int _busyTimeoutMilliseconds;
    private readonly TimeSpan _leaseDuration;

    public AjureStore(StorageOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (string.IsNullOrWhiteSpace(options.DataPath))
        {
            throw new ArgumentException("Ajure:Storage:DataPath is required.", nameof(options));
        }

        if (!Path.IsPathFullyQualified(options.DataPath))
        {
            throw new ArgumentException(
                "Ajure:Storage:DataPath must be an absolute path.",
                nameof(options));
        }

        if (options.BusyTimeoutMilliseconds <= 0 || options.LeaseSeconds <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "Storage timeout values must be positive.");
        }

        var dataPath = Path.GetFullPath(options.DataPath);
        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = dataPath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared,
            Pooling = true,
            DefaultTimeout = Math.Max(1, options.BusyTimeoutMilliseconds / 1_000)
        }.ToString();
        _busyTimeoutMilliseconds = options.BusyTimeoutMilliseconds;
        _leaseDuration = TimeSpan.FromSeconds(options.LeaseSeconds);
    }

    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        var dataPath = new SqliteConnectionStringBuilder(_connectionString).DataSource;
        var directory = Path.GetDirectoryName(dataPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await ExecuteAsync(connection, "PRAGMA journal_mode = WAL;", cancellationToken).ConfigureAwait(false);
        await ExecuteAsync(
                connection,
                """
                CREATE TABLE IF NOT EXISTS Projects (
                    Id TEXT PRIMARY KEY,
                    CreatedAt INTEGER NOT NULL,
                    Data TEXT NOT NULL
                );
                CREATE TABLE IF NOT EXISTS Versions (
                    Id TEXT PRIMARY KEY,
                    ProjectId TEXT NOT NULL,
                    Number INTEGER NOT NULL,
                    Data TEXT NOT NULL
                );
                CREATE INDEX IF NOT EXISTS IX_Versions_Project_Number ON Versions (ProjectId, Number DESC);
                CREATE TABLE IF NOT EXISTS Decisions (
                    ProjectId TEXT NOT NULL,
                    Id TEXT NOT NULL,
                    Data TEXT NOT NULL,
                    PRIMARY KEY (ProjectId, Id)
                );
                CREATE TABLE IF NOT EXISTS Jobs (
                    Id TEXT PRIMARY KEY,
                    ProjectId TEXT NOT NULL,
                    CreatedAt INTEGER NOT NULL,
                    Data TEXT NOT NULL
                );
                CREATE INDEX IF NOT EXISTS IX_Jobs_Project_CreatedAt ON Jobs (ProjectId, CreatedAt DESC);
                CREATE TABLE IF NOT EXISTS JobEvents (
                    JobId TEXT NOT NULL,
                    Sequence INTEGER NOT NULL,
                    Data TEXT NOT NULL,
                    PRIMARY KEY (JobId, Sequence),
                    FOREIGN KEY (JobId) REFERENCES Jobs (Id)
                );
                CREATE TABLE IF NOT EXISTS Artifacts (
                    Id TEXT PRIMARY KEY,
                    SpecVersionId TEXT NOT NULL,
                    Path TEXT NOT NULL,
                    Data TEXT NOT NULL
                );
                CREATE INDEX IF NOT EXISTS IX_Artifacts_SpecVersion_Path ON Artifacts (SpecVersionId, Path);
                CREATE TABLE IF NOT EXISTS ValidationRuns (
                    Id TEXT PRIMARY KEY,
                    SpecVersionId TEXT NOT NULL,
                    StartedAt INTEGER NOT NULL,
                    Data TEXT NOT NULL
                );
                CREATE INDEX IF NOT EXISTS IX_ValidationRuns_SpecVersion_StartedAt ON ValidationRuns (SpecVersionId, StartedAt DESC);
                CREATE TABLE IF NOT EXISTS Blobs (
                    Name TEXT PRIMARY KEY,
                    Content BLOB NOT NULL,
                    ContentType TEXT NOT NULL
                );
                CREATE TABLE IF NOT EXISTS QueueMessages (
                    Id TEXT PRIMARY KEY,
                    Data TEXT NOT NULL,
                    EnqueuedAt INTEGER NOT NULL,
                    VisibleAt INTEGER NOT NULL,
                    LeaseToken TEXT NULL,
                    LeaseExpiresAt INTEGER NULL,
                    DequeueCount INTEGER NOT NULL
                );
                CREATE INDEX IF NOT EXISTS IX_QueueMessages_VisibleAt ON QueueMessages (VisibleAt, EnqueuedAt);
                CREATE TABLE IF NOT EXISTS PoisonMessages (
                    Id TEXT PRIMARY KEY,
                    Data TEXT NOT NULL,
                    ErrorType TEXT NOT NULL,
                    FailedAt INTEGER NOT NULL
                );
                CREATE TABLE IF NOT EXISTS ModelProviderCredentials (
                    ProviderId TEXT PRIMARY KEY,
                    ProtectedApiKey TEXT NOT NULL,
                    Model TEXT NOT NULL,
                    UpdatedAt INTEGER NOT NULL
                );
                """,
                cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task CreateProjectAsync(ProjectRecord project, CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var command = Command(
            connection,
            "INSERT INTO Projects (Id, CreatedAt, Data) VALUES ($id, $createdAt, $data);",
            ("$id", Key(project.Id)),
            ("$createdAt", Milliseconds(project.CreatedAt)),
            ("$data", Serialize(project)));
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<ProjectRecord>> ListProjectsAsync(CancellationToken cancellationToken) =>
        await ListAsync<ProjectRecord>(
                "SELECT Data FROM Projects ORDER BY CreatedAt DESC;",
                cancellationToken)
            .ConfigureAwait(false);

    public Task<ProjectRecord?> GetProjectAsync(Guid projectId, CancellationToken cancellationToken) =>
        GetAsync<ProjectRecord>("Projects", Key(projectId), cancellationToken);

    public async Task SaveVersionAsync(SpecVersionRecord version, CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await UpsertAsync(
                connection,
                """
                INSERT INTO Versions (Id, ProjectId, Number, Data) VALUES ($id, $projectId, $number, $data)
                ON CONFLICT (Id) DO UPDATE SET ProjectId = excluded.ProjectId, Number = excluded.Number, Data = excluded.Data;
                """,
                cancellationToken,
                ("$id", Key(version.Id)),
                ("$projectId", Key(version.ProjectId)),
                ("$number", version.Number),
                ("$data", Serialize(version)))
            .ConfigureAwait(false);
    }

    public Task<SpecVersionRecord?> GetVersionAsync(Guid versionId, CancellationToken cancellationToken) =>
        GetAsync<SpecVersionRecord>("Versions", Key(versionId), cancellationToken);

    public async Task<IReadOnlyList<SpecVersionRecord>> ListVersionsAsync(
        Guid projectId,
        CancellationToken cancellationToken) =>
        await ListAsync<SpecVersionRecord>(
                "SELECT Data FROM Versions WHERE ProjectId = $projectId ORDER BY Number DESC;",
                cancellationToken,
                ("$projectId", Key(projectId)))
            .ConfigureAwait(false);

    public async Task SaveDecisionAsync(DecisionRecord decision, CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await UpsertAsync(
                connection,
                """
                INSERT INTO Decisions (ProjectId, Id, Data) VALUES ($projectId, $id, $data)
                ON CONFLICT (ProjectId, Id) DO UPDATE SET Data = excluded.Data;
                """,
                cancellationToken,
                ("$projectId", Key(decision.ProjectId)),
                ("$id", decision.Id),
                ("$data", Serialize(decision)))
            .ConfigureAwait(false);
    }

    public async Task<DecisionRecord?> GetDecisionAsync(
        Guid projectId,
        string decisionId,
        CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var command = Command(
            connection,
            "SELECT Data FROM Decisions WHERE ProjectId = $projectId AND Id = $id;",
            ("$projectId", Key(projectId)),
            ("$id", decisionId));
        return await ReadOneAsync<DecisionRecord>(command, cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<DecisionRecord>> ListDecisionsAsync(
        Guid projectId,
        CancellationToken cancellationToken) =>
        await ListAsync<DecisionRecord>(
                "SELECT Data FROM Decisions WHERE ProjectId = $projectId ORDER BY Id COLLATE BINARY;",
                cancellationToken,
                ("$projectId", Key(projectId)))
            .ConfigureAwait(false);

    public async Task SaveJobAsync(JobRecord job, CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await UpsertAsync(
                connection,
                """
                INSERT INTO Jobs (Id, ProjectId, CreatedAt, Data) VALUES ($id, $projectId, $createdAt, $data)
                ON CONFLICT (Id) DO UPDATE SET ProjectId = excluded.ProjectId, CreatedAt = excluded.CreatedAt, Data = excluded.Data;
                """,
                cancellationToken,
                ("$id", Key(job.Id)),
                ("$projectId", Key(job.ProjectId)),
                ("$createdAt", Milliseconds(job.CreatedAt)),
                ("$data", Serialize(job)))
            .ConfigureAwait(false);
    }

    public Task<JobRecord?> GetJobAsync(Guid jobId, CancellationToken cancellationToken) =>
        GetAsync<JobRecord>("Jobs", Key(jobId), cancellationToken);

    public async Task<IReadOnlyList<JobRecord>> ListJobsAsync(
        Guid projectId,
        CancellationToken cancellationToken) =>
        await ListAsync<JobRecord>(
                "SELECT Data FROM Jobs WHERE ProjectId = $projectId ORDER BY CreatedAt DESC;",
                cancellationToken,
                ("$projectId", Key(projectId)))
            .ConfigureAwait(false);

    public async Task EnqueueAsync(JobMessage message, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await UpsertAsync(
                connection,
                """
                INSERT INTO QueueMessages (Id, Data, EnqueuedAt, VisibleAt, LeaseToken, LeaseExpiresAt, DequeueCount)
                VALUES ($id, $data, $now, $now, NULL, NULL, 0);
                """,
                cancellationToken,
                ("$id", Guid.NewGuid().ToString("N")),
                ("$data", Serialize(message)),
                ("$now", Milliseconds(now)))
            .ConfigureAwait(false);
    }

    public async Task<DequeuedJob?> DequeueAsync(CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await BeginImmediateAsync(connection, cancellationToken).ConfigureAwait(false);
        try
        {
            var now = DateTimeOffset.UtcNow;
            await using var select = Command(
                connection,
                """
                SELECT Id, Data, DequeueCount FROM QueueMessages
                WHERE VisibleAt <= $now AND (LeaseExpiresAt IS NULL OR LeaseExpiresAt <= $now)
                ORDER BY VisibleAt, EnqueuedAt, Id
                LIMIT 1;
                """,
                ("$now", Milliseconds(now)));
            await using var reader = await select.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                await CommitAsync(connection, cancellationToken).ConfigureAwait(false);
                return null;
            }

            var messageId = reader.GetString(0);
            var message = Deserialize<JobMessage>(reader.GetString(1));
            var dequeueCount = checked(reader.GetInt64(2) + 1);
            var popReceipt = Guid.NewGuid().ToString("N");
            await reader.DisposeAsync().ConfigureAwait(false);
            await using var update = Command(
                connection,
                """
                UPDATE QueueMessages
                SET LeaseToken = $leaseToken, LeaseExpiresAt = $leaseExpiresAt, DequeueCount = $dequeueCount
                WHERE Id = $id;
                """,
                ("$leaseToken", popReceipt),
                ("$leaseExpiresAt", Milliseconds(now + _leaseDuration)),
                ("$dequeueCount", dequeueCount),
                ("$id", messageId));
            await update.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            await CommitAsync(connection, cancellationToken).ConfigureAwait(false);
            return new DequeuedJob(message, messageId, popReceipt, dequeueCount);
        }
        catch
        {
            await RollbackAsync(connection).ConfigureAwait(false);
            throw;
        }
    }

    public async Task DeleteMessageAsync(DequeuedJob job, CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var command = Command(
            connection,
            "DELETE FROM QueueMessages WHERE Id = $id AND LeaseToken = $leaseToken;",
            ("$id", job.MessageId),
            ("$leaseToken", job.PopReceipt));
        EnsureLease(await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false));
    }

    public async Task ReleaseForRetryAsync(
        DequeuedJob job,
        TimeSpan delay,
        CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(delay, TimeSpan.Zero);

        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var command = Command(
            connection,
            """
            UPDATE QueueMessages
            SET VisibleAt = $visibleAt, LeaseToken = NULL, LeaseExpiresAt = NULL
            WHERE Id = $id AND LeaseToken = $leaseToken;
            """,
            ("$visibleAt", Milliseconds(DateTimeOffset.UtcNow + delay)),
            ("$id", job.MessageId),
            ("$leaseToken", job.PopReceipt));
        EnsureLease(await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false));
    }

    public async Task MoveToPoisonAsync(
        DequeuedJob job,
        string errorType,
        CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await BeginImmediateAsync(connection, cancellationToken).ConfigureAwait(false);
        try
        {
            await using var delete = Command(
                connection,
                "DELETE FROM QueueMessages WHERE Id = $id AND LeaseToken = $leaseToken;",
                ("$id", job.MessageId),
                ("$leaseToken", job.PopReceipt));
            EnsureLease(await delete.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false));

            var failedAt = DateTimeOffset.UtcNow;
            await UpsertAsync(
                    connection,
                    "INSERT INTO PoisonMessages (Id, Data, ErrorType, FailedAt) VALUES ($id, $data, $errorType, $failedAt);",
                    cancellationToken,
                    ("$id", job.MessageId),
                    ("$data", Serialize(new PoisonJobMessage(job.Message, errorType, failedAt))),
                    ("$errorType", errorType),
                    ("$failedAt", Milliseconds(failedAt)))
                .ConfigureAwait(false);
            await CommitAsync(connection, cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            await RollbackAsync(connection).ConfigureAwait(false);
            throw;
        }
    }

    public async Task SaveModelProviderCredentialAsync(
        ModelProviderCredentialRecord credential,
        CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await UpsertAsync(
                connection,
                """
                INSERT INTO ModelProviderCredentials (ProviderId, ProtectedApiKey, Model, UpdatedAt)
                VALUES ($providerId, $protectedApiKey, $model, $updatedAt)
                ON CONFLICT (ProviderId) DO UPDATE SET
                    ProtectedApiKey = excluded.ProtectedApiKey,
                    Model = excluded.Model,
                    UpdatedAt = excluded.UpdatedAt;
                """,
                cancellationToken,
                ("$providerId", credential.ProviderId),
                ("$protectedApiKey", credential.ProtectedApiKey),
                ("$model", credential.Model),
                ("$updatedAt", Milliseconds(credential.UpdatedAt)))
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<ModelProviderCredentialRecord>> ListModelProviderCredentialsAsync(
        CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var command = Command(
            connection,
            """
            SELECT ProviderId, ProtectedApiKey, Model, UpdatedAt
            FROM ModelProviderCredentials
            ORDER BY ProviderId COLLATE BINARY;
            """);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        var credentials = new List<ModelProviderCredentialRecord>();
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            credentials.Add(new ModelProviderCredentialRecord(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetString(2),
                DateTimeOffset.FromUnixTimeMilliseconds(reader.GetInt64(3))));
        }

        return credentials;
    }

    public async Task<bool> DeleteModelProviderCredentialAsync(
        string providerId,
        CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var command = Command(
            connection,
            "DELETE FROM ModelProviderCredentials WHERE ProviderId = $providerId;",
            ("$providerId", providerId));
        return await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false) == 1;
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
        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await BeginImmediateAsync(connection, cancellationToken).ConfigureAwait(false);
        try
        {
            await using var select = Command(connection, "SELECT Data FROM Jobs WHERE Id = $id;", ("$id", Key(jobId)));
            var job = await ReadOneAsync<JobRecord>(select, cancellationToken).ConfigureAwait(false)
                ?? throw new InvalidOperationException($"Job '{jobId}' was not found.");
            var sequence = checked(job.LastSequence + 1);
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
            await UpsertAsync(
                    connection,
                    "INSERT INTO JobEvents (JobId, Sequence, Data) VALUES ($jobId, $sequence, $data);",
                    cancellationToken,
                    ("$jobId", Key(jobId)),
                    ("$sequence", sequence),
                    ("$data", Serialize(jobEvent)))
                .ConfigureAwait(false);
            await UpsertAsync(
                    connection,
                    "UPDATE Jobs SET Data = $data WHERE Id = $id;",
                    cancellationToken,
                    ("$id", Key(jobId)),
                    ("$data", Serialize(job with { LastSequence = sequence })))
                .ConfigureAwait(false);
            await CommitAsync(connection, cancellationToken).ConfigureAwait(false);
            return jobEvent;
        }
        catch
        {
            await RollbackAsync(connection).ConfigureAwait(false);
            throw;
        }
    }

    public async Task<IReadOnlyList<JobEventRecord>> ListEventsAsync(
        Guid jobId,
        long afterSequence,
        CancellationToken cancellationToken) =>
        await ListAsync<JobEventRecord>(
                "SELECT Data FROM JobEvents WHERE JobId = $jobId AND Sequence > $afterSequence ORDER BY Sequence;",
                cancellationToken,
                ("$jobId", Key(jobId)),
                ("$afterSequence", afterSequence))
            .ConfigureAwait(false);

    public async Task SaveArtifactAsync(ArtifactRecord artifact, CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await UpsertAsync(
                connection,
                """
                INSERT INTO Artifacts (Id, SpecVersionId, Path, Data) VALUES ($id, $specVersionId, $path, $data)
                ON CONFLICT (Id) DO UPDATE SET SpecVersionId = excluded.SpecVersionId, Path = excluded.Path, Data = excluded.Data;
                """,
                cancellationToken,
                ("$id", Key(artifact.Id)),
                ("$specVersionId", Key(artifact.SpecVersionId)),
                ("$path", artifact.Path),
                ("$data", Serialize(artifact)))
            .ConfigureAwait(false);
    }

    public Task<ArtifactRecord?> GetArtifactAsync(Guid artifactId, CancellationToken cancellationToken) =>
        GetAsync<ArtifactRecord>("Artifacts", Key(artifactId), cancellationToken);

    public async Task<IReadOnlyList<ArtifactRecord>> ListArtifactsAsync(
        Guid versionId,
        CancellationToken cancellationToken) =>
        await ListAsync<ArtifactRecord>(
                "SELECT Data FROM Artifacts WHERE SpecVersionId = $specVersionId ORDER BY Path COLLATE BINARY;",
                cancellationToken,
                ("$specVersionId", Key(versionId)))
            .ConfigureAwait(false);

    public async Task PutBlobAsync(
        string blobName,
        BinaryData content,
        string contentType,
        CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await UpsertAsync(
                connection,
                """
                INSERT INTO Blobs (Name, Content, ContentType) VALUES ($name, $content, $contentType)
                ON CONFLICT (Name) DO UPDATE SET Content = excluded.Content, ContentType = excluded.ContentType;
                """,
                cancellationToken,
                ("$name", blobName),
                ("$content", content.ToArray()),
                ("$contentType", contentType))
            .ConfigureAwait(false);
    }

    public async Task<BinaryData?> GetBlobAsync(string blobName, CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var command = Command(connection, "SELECT Content FROM Blobs WHERE Name = $name;", ("$name", blobName));
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        return await reader.ReadAsync(cancellationToken).ConfigureAwait(false)
            ? BinaryData.FromBytes(reader.GetFieldValue<byte[]>(0))
            : null;
    }

    public async Task SaveValidationRunAsync(
        ValidationRunRecord run,
        CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await UpsertAsync(
                connection,
                """
                INSERT INTO ValidationRuns (Id, SpecVersionId, StartedAt, Data) VALUES ($id, $specVersionId, $startedAt, $data)
                ON CONFLICT (Id) DO UPDATE SET SpecVersionId = excluded.SpecVersionId, StartedAt = excluded.StartedAt, Data = excluded.Data;
                """,
                cancellationToken,
                ("$id", Key(run.Id)),
                ("$specVersionId", Key(run.SpecVersionId)),
                ("$startedAt", Milliseconds(run.StartedAt)),
                ("$data", Serialize(run)))
            .ConfigureAwait(false);
    }

    public Task<ValidationRunRecord?> GetValidationRunAsync(
        Guid runId,
        CancellationToken cancellationToken) =>
        GetAsync<ValidationRunRecord>("ValidationRuns", Key(runId), cancellationToken);

    public async Task<ValidationRunRecord?> GetLatestValidationRunAsync(
        Guid versionId,
        CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var command = Command(
            connection,
            """
            SELECT Data FROM ValidationRuns
            WHERE SpecVersionId = $specVersionId
            ORDER BY StartedAt DESC
            LIMIT 1;
            """,
            ("$specVersionId", Key(versionId)));
        return await ReadOneAsync<ValidationRunRecord>(command, cancellationToken).ConfigureAwait(false);
    }

    private async Task<T?> GetAsync<T>(string table, string id, CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var command = Command(connection, $"SELECT Data FROM {table} WHERE Id = $id;", ("$id", id));
        return await ReadOneAsync<T>(command, cancellationToken).ConfigureAwait(false);
    }

    private async Task<IReadOnlyList<T>> ListAsync<T>(
        string sql,
        CancellationToken cancellationToken,
        params (string Name, object? Value)[] parameters)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var command = Command(connection, sql, parameters);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        var items = new List<T>();
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            items.Add(Deserialize<T>(reader.GetString(0)));
        }

        return items;
    }

    private async Task<SqliteConnection> OpenConnectionAsync(CancellationToken cancellationToken)
    {
        var connection = new SqliteConnection(_connectionString);
        try
        {
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            await ExecuteAsync(
                    connection,
                    $"PRAGMA foreign_keys = ON; PRAGMA busy_timeout = {_busyTimeoutMilliseconds.ToString(CultureInfo.InvariantCulture)};",
                    cancellationToken)
                .ConfigureAwait(false);
            return connection;
        }
        catch
        {
            await connection.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    private static SqliteCommand Command(
        SqliteConnection connection,
        string sql,
        params (string Name, object? Value)[] parameters)
    {
        var command = connection.CreateCommand();
        command.CommandText = sql;
        foreach (var (name, value) in parameters)
        {
            command.Parameters.AddWithValue(name, value ?? DBNull.Value);
        }

        return command;
    }

    private static async Task UpsertAsync(
        SqliteConnection connection,
        string sql,
        CancellationToken cancellationToken,
        params (string Name, object? Value)[] parameters)
    {
        await using var command = Command(connection, sql, parameters);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task ExecuteAsync(SqliteConnection connection, string sql, CancellationToken cancellationToken)
    {
        await using var command = Command(connection, sql);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task<T?> ReadOneAsync<T>(SqliteCommand command, CancellationToken cancellationToken)
    {
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        return await reader.ReadAsync(cancellationToken).ConfigureAwait(false)
            ? Deserialize<T>(reader.GetString(0))
            : default;
    }

    private static Task BeginImmediateAsync(SqliteConnection connection, CancellationToken cancellationToken) =>
        ExecuteAsync(connection, "BEGIN IMMEDIATE;", cancellationToken);

    private static Task CommitAsync(SqliteConnection connection, CancellationToken cancellationToken) =>
        ExecuteAsync(connection, "COMMIT;", cancellationToken);

    private static async Task RollbackAsync(SqliteConnection connection)
    {
        try
        {
            await ExecuteAsync(connection, "ROLLBACK;", CancellationToken.None).ConfigureAwait(false);
        }
        catch (SqliteException)
        {
        }
    }

    private static void EnsureLease(int affectedRows)
    {
        if (affectedRows != 1)
        {
            throw new InvalidOperationException("The queue message lease is no longer owned.");
        }
    }

    private static string Serialize<T>(T value) =>
        JsonSerializer.Serialize(value, JsonDefaults.Options);

    private static T Deserialize<T>(string json) =>
        JsonSerializer.Deserialize<T>(json, JsonDefaults.Options)
        ?? throw new InvalidDataException($"Stored entity could not be read as {typeof(T).Name}.");

    private static long Milliseconds(DateTimeOffset value) => value.ToUnixTimeMilliseconds();

    private static string Key(Guid id) => id.ToString("N");
}

public static class JsonDefaults
{
    public static JsonSerializerOptions Options { get; } = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = false
    };
}
