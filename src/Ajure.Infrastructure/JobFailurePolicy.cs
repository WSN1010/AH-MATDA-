using System.Text.Json;
using Ajure.Agent;
using Azure;

namespace Ajure.Infrastructure;

public sealed record JobFailure(string Code, string Message, bool Retryable);

public static class JobFailurePolicy
{
    public static JobFailure Classify(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        return exception switch
        {
            ModelDiversityException => new(
                "model_diversity_unavailable",
                "At least two configured evaluation models must be available.",
                Retryable: false),
            JsonException or InvalidDataException => new(
                "model_output_invalid",
                "A required model response was invalid.",
                Retryable: false),
            TimeoutException => new(
                "dependency_timeout",
                "A required dependency timed out.",
                Retryable: true),
            HttpRequestException => new(
                "dependency_unavailable",
                "A required dependency was unavailable.",
                Retryable: true),
            RequestFailedException request when request.Status is 408 or 429 or >= 500 => new(
                "storage_transient",
                "Storage temporarily rejected the operation.",
                Retryable: true),
            RequestFailedException => new(
                "storage_failure",
                "Storage rejected the operation.",
                Retryable: false),
            InvalidOperationException => new(
                "job_invalid_operation",
                "The job could not run in its current state.",
                Retryable: false),
            _ => new(
                "job_failed",
                "The job failed.",
                Retryable: false)
        };
    }
}
