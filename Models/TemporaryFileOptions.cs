namespace FacturXValidatorSaas.Models;

public sealed class TemporaryFileOptions
{
    public string UploadPath { get; set; } = "/app/data/uploads";

    public int RetentionHours { get; set; } = 24;

    public int CleanupIntervalMinutes { get; set; } = 60;
}
