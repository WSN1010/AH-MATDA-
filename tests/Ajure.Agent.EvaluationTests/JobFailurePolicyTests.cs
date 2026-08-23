using System.Net;
using System.Text.Json;
using Ajure.Agent;
using Ajure.Infrastructure;

namespace Ajure.Agent.EvaluationTests;

public sealed class JobFailurePolicyTests
{
    [Fact]
    public void ModelDiversityFailureIsNotRetried()
    {
        var failure = JobFailurePolicy.Classify(new ModelDiversityException());

        Assert.Equal("model_diversity_unavailable", failure.Code);
        Assert.False(failure.Retryable);
    }

    [Fact]
    public void InvalidModelOutputIsNotRetried()
    {
        var failure = JobFailurePolicy.Classify(new JsonException());

        Assert.Equal("model_output_invalid", failure.Code);
        Assert.False(failure.Retryable);
    }

    [Fact]
    public void RateLimitedModelProviderIsRetried()
    {
        var failure = JobFailurePolicy.Classify(
            new ModelProviderException(
                "openai",
                HttpStatusCode.TooManyRequests,
                retryable: true));

        Assert.Equal("model_provider_transient", failure.Code);
        Assert.True(failure.Retryable);
    }

    [Fact]
    public void AuthenticationFailureIsNotRetried()
    {
        var failure = JobFailurePolicy.Classify(
            new ModelProviderException(
                "anthropic",
                HttpStatusCode.Unauthorized,
                retryable: false));

        Assert.Equal("model_provider_rejected", failure.Code);
        Assert.False(failure.Retryable);
    }

    [Fact]
    public void DependencyTimeoutIsRetried()
    {
        var failure = JobFailurePolicy.Classify(new TimeoutException());

        Assert.Equal("dependency_timeout", failure.Code);
        Assert.True(failure.Retryable);
    }
}
