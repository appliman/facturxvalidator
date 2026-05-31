using FacturXValidator.Models;
using FacturXValidator.Services;
using Microsoft.AspNetCore.Components.Forms;

namespace FacturXValidator.Tests;

internal sealed class CapturingFileStorageService : IFileStorageService
{
    public List<StoredFile> StoredFiles { get; } = [];

    public List<StoredFile> DeletedFiles { get; } = [];

    public async Task<StoredFile> StoreAsync(IBrowserFile file, CancellationToken cancellationToken)
    {
        Assert.IsTrue(file.Name.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase));
        Assert.AreEqual("application/pdf", file.ContentType);

        await using var stream = file.OpenReadStream(cancellationToken: cancellationToken);
        using var reader = new StreamReader(stream);
        var content = await reader.ReadToEndAsync(cancellationToken);
        Assert.IsTrue(content.StartsWith("%PDF-1.7", StringComparison.Ordinal));

        var storedFile = new StoredFile
        {
            SafeDisplayName = file.Name,
            ServerFileName = $"{Path.GetFileNameWithoutExtension(file.Name)}.uploaded.pdf",
            FullPath = Path.Combine(Path.GetTempPath(), $"{Path.GetFileNameWithoutExtension(file.Name)}.uploaded.pdf"),
            UploadedAt = new DateTimeOffset(2026, 5, 31, 8, 0, 0, TimeSpan.Zero)
        };

        StoredFiles.Add(storedFile);
        return storedFile;
    }

    public Task DeleteAsync(StoredFile file, CancellationToken cancellationToken)
    {
        DeletedFiles.Add(file);
        return Task.CompletedTask;
    }
}
