using System.Text.Json;
using Ajure.Infrastructure;

namespace Ajure.Api;

public static class SseEndpoint
{
    public static async Task<IResult> StreamAsync(
        Guid jobId,
        HttpContext context,
        AjureStore store,
        CancellationToken cancellationToken)
    {
        var job = await store.GetJobAsync(jobId, cancellationToken).ConfigureAwait(false);
        if (job is null)
        {
            return ApiProblems.NotFound(context, "job_not_found", "The job was not found.");
        }

        var cursor = ParseCursor(context);
        context.Response.Headers.CacheControl = "no-cache";
        context.Response.Headers.Append("X-Accel-Buffering", "no");
        context.Response.ContentType = "text/event-stream";
        var lastHeartbeat = DateTimeOffset.UtcNow;

        while (!cancellationToken.IsCancellationRequested)
        {
            var events = await store.ListEventsAsync(jobId, cursor, cancellationToken).ConfigureAwait(false);
            foreach (var jobEvent in events)
            {
                await context.Response
                    .WriteAsync(
                        $"id: {jobEvent.Sequence}\nevent: {jobEvent.EventType}\ndata: {JsonSerializer.Serialize(jobEvent, JsonDefaults.Options)}\n\n",
                        cancellationToken)
                    .ConfigureAwait(false);
                cursor = jobEvent.Sequence;
            }

            if (events.Count > 0)
            {
                await context.Response.Body.FlushAsync(cancellationToken).ConfigureAwait(false);
            }

            job = await store.GetJobAsync(jobId, cancellationToken).ConfigureAwait(false);
            if (job is null || IsTerminal(job.Status) && cursor >= job.LastSequence)
            {
                break;
            }

            if (DateTimeOffset.UtcNow - lastHeartbeat >= TimeSpan.FromSeconds(15))
            {
                await context.Response.WriteAsync(": heartbeat\n\n", cancellationToken).ConfigureAwait(false);
                await context.Response.Body.FlushAsync(cancellationToken).ConfigureAwait(false);
                lastHeartbeat = DateTimeOffset.UtcNow;
            }

            await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken).ConfigureAwait(false);
        }

        return Results.Empty;
    }

    private static long ParseCursor(HttpContext context)
    {
        var raw = context.Request.Headers["Last-Event-ID"].FirstOrDefault()
            ?? context.Request.Query["after"].FirstOrDefault();
        return long.TryParse(
            raw,
            System.Globalization.NumberStyles.None,
            System.Globalization.CultureInfo.InvariantCulture,
            out var cursor)
            ? Math.Max(cursor, 0)
            : 0;
    }

    private static bool IsTerminal(JobStatus status) =>
        status is JobStatus.Succeeded or JobStatus.Failed;
}
