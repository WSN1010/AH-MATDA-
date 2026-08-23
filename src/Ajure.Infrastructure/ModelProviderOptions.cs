namespace Ajure.Infrastructure;

public sealed class ModelProviderOptions
{
    public const string SectionName = "Ajure:Models";
    public const string ModelPoolSectionName = "Ajure:Review:ModelPool";

    public int SessionTimeoutSeconds { get; set; } = 120;

    public int MaxOutputTokens { get; set; } = 16_384;

    public ModelEndpointOptions OpenAI { get; set; } = new()
    {
        BaseUrl = "https://api.openai.com/v1/",
        Model = "gpt-5.4-mini"
    };

    public ModelEndpointOptions Anthropic { get; set; } = new()
    {
        BaseUrl = "https://api.anthropic.com/v1/",
        Model = "claude-sonnet-5"
    };

    public ModelEndpointOptions Gemini { get; set; } = new()
    {
        BaseUrl = "https://generativelanguage.googleapis.com/v1beta/",
        Model = "gemini-2.5-pro"
    };
}

public sealed class ModelEndpointOptions
{
    public string ApiKey { get; set; } = string.Empty;

    public string BaseUrl { get; set; } = string.Empty;

    public string Model { get; set; } = string.Empty;
}
