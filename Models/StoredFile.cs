namespace FacturXValidatorSaas.Models;

public sealed class StoredFile
{
    public required string SafeDisplayName { get; init; }

    public required string ServerFileName { get; init; }

    public required string FullPath { get; init; }

    public DateTimeOffset UploadedAt { get; init; } = DateTimeOffset.UtcNow;
}
