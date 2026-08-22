using Ajure.Infrastructure;

namespace Ajure.Worker;

public sealed class Worker(
    AjureStore store,
    JobProcessor processor,
    ILogger<Worker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        WorkerLog.Started(logger);

        while (!stoppingToken.IsCancellationRequested)
        {
            var dequeued = await store.DequeueAsync(stoppingToken).ConfigureAwait(false);
            if (dequeued is null)
            {
                await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken).ConfigureAwait(false);
                continue;
            }

            try
            {
                await processor.ProcessAsync(dequeued.Message, stoppingToken).ConfigureAwait(false);
                await store.DeleteMessageAsync(dequeued, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                var failure = JobFailurePolicy.Classify(exception);
                WorkerLog.JobFailed(
                    logger,
                    dequeued.Message.JobId,
                    dequeued.DequeueCount,
                    failure.Code,
                    exception.GetType().Name);
                await HandleFailureAsync(
                        dequeued,
                        failure,
                        exception.GetType().Name,
                        stoppingToken)
                    .ConfigureAwait(false);
            }
        }
    }

    private async Task HandleFailureAsync(
        DequeuedJob dequeued,
        JobFailure failure,
        string exceptionType,
        CancellationToken cancellationToken)
    {
        var job = await store.GetJobAsync(dequeued.Message.JobId, cancellationToken).ConfigureAwait(false);
        if (job is null)
        {
            await store.MoveToPoisonAsync(
                dequeued,
                exceptionType,
                cancellationToken).ConfigureAwait(false);
            return;
        }

        if (failure.Retryable && dequeued.DequeueCount < 5)
        {
            var retryEvent = await store.AppendEventAsync(
                    job.Id,
                    "job.retrying",
                    "worker",
                    "retrying",
                    $"Job attempt {dequeued.DequeueCount} failed and will be retried.",
                    retryable: true,
                    cancellationToken)
                .ConfigureAwait(false);
            await store.SaveJobAsync(
                job with
                {
                    Status = JobStatus.Queued,
                    LastSequence = retryEvent.Sequence,
                    ErrorCode = failure.Code,
                    ErrorMessage = failure.Message
                },
                cancellationToken).ConfigureAwait(false);
            var delay = TimeSpan.FromSeconds(
                1 << (int)Math.Min(dequeued.DequeueCount, 4));
            await store
                .ReleaseForRetryAsync(dequeued, delay, cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        await store.AppendEventAsync(
                job.Id,
                "job.failed",
                "worker",
                "failed",
                failure.Retryable
                    ? "Job failed after the maximum retry count."
                    : "Job failed with a non-retryable error.",
                retryable: false,
                cancellationToken)
            .ConfigureAwait(false);
        var current = await store.GetJobAsync(job.Id, cancellationToken).ConfigureAwait(false) ?? job;
        await store.SaveJobAsync(
            current with
            {
                Status = JobStatus.Failed,
                ErrorCode = failure.Code,
                ErrorMessage = failure.Message,
                CompletedAt = DateTimeOffset.UtcNow
            },
            cancellationToken).ConfigureAwait(false);
        await store.MoveToPoisonAsync(
            dequeued,
            exceptionType,
            cancellationToken).ConfigureAwait(false);
    }
}

internal static partial class WorkerLog
{
    [LoggerMessage(EventId = 1, Level = LogLevel.Information, Message = "Ajure worker started")]
    internal static partial void Started(ILogger logger);

    [LoggerMessage(
        EventId = 2,
        Level = LogLevel.Warning,
        Message = "Job {JobId} attempt {Attempt} failed with {ErrorCode} ({ExceptionType})")]
    internal static partial void JobFailed(
        ILogger logger,
        Guid jobId,
        long attempt,
        string errorCode,
        string exceptionType);
}
