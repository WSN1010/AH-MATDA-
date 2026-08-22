namespace Ajure.Infrastructure;

public sealed class CopilotRuntimeOptions
{
    public const string SectionName = "Ajure:Copilot";
    public const string ModelPoolSectionName = "Ajure:Review:ModelPool";

    public string HomeDirectory { get; set; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Ajure", "copilot");

    public bool UseLoggedInUser { get; set; } = true;

    public int SessionTimeoutSeconds { get; set; } = 120;

    public string[] ModelPool { get; set; } = [];
}
