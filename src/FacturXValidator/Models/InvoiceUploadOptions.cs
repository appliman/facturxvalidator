namespace FacturXValidator.Models;

public sealed class InvoiceUploadOptions
{
    public int MaxFileSizeMb { get; set; } = 20;

    public int MaxFilesPerBatch { get; set; } = 10;

    public string[] AllowedContentTypes { get; set; } =
    [
        "application/pdf",
        "application/x-pdf"
    ];

    public long MaxFileSizeBytes => Math.Max(1, MaxFileSizeMb) * 1024L * 1024L;
}
