namespace Ajure.Infrastructure;

public sealed class StorageOptions
{
    public const string SectionName = "Ajure:Storage";

    public string DataPath { get; set; } =
        Environment.GetEnvironmentVariable("AJURE_DATA_PATH")
        ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Ajure",
            "ajure.db");

    public int BusyTimeoutMilliseconds { get; set; } = 5_000;

    public int LeaseSeconds { get; set; } = 30 * 60;
}
