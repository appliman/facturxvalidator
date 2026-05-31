using System.Security.Cryptography;
using FacturXValidatorSaas.Models;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.Options;

namespace FacturXValidatorSaas.Services;

public sealed class LocalTemporaryFileStorageService(
    IOptions<InvoiceUploadOptions> uploadOptions,
    IOptions<TemporaryFileOptions> temporaryFileOptions,
    ILogger<LocalTemporaryFileStorageService> logger) : IFileStorageService
{
    private static readonly byte[] PdfSignature = "%PDF-"u8.ToArray();

    public async Task<StoredFile> StoreAsync(IBrowserFile file, CancellationToken cancellationToken)
    {
        var upload = uploadOptions.Value;

        if (!string.Equals(Path.GetExtension(file.Name), ".pdf", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidInvoiceUploadException("Seuls les fichiers PDF sont acceptés.");
        }

        if (file.Size <= 0 || file.Size > upload.MaxFileSizeBytes)
        {
            throw new InvalidInvoiceUploadException($"Le fichier dépasse la limite configurée de {upload.MaxFileSizeMb} Mo.");
        }

        if (!upload.AllowedContentTypes.Contains(file.ContentType, StringComparer.OrdinalIgnoreCase))
        {
            throw new InvalidInvoiceUploadException($"Type de contenu refusé : {file.ContentType}. Le fichier doit être envoyé comme application/pdf.");
        }

        var uploadRoot = ResolveUploadRoot();
        Directory.CreateDirectory(uploadRoot);

        var safeDisplayName = SanitizeDisplayName(file.Name);
        var serverFileName = $"{DateTimeOffset.UtcNow:yyyyMMddHHmmssfff}-{RandomNumberGenerator.GetHexString(12).ToLowerInvariant()}.pdf";
        var destination = EnsurePathInsideRoot(uploadRoot, Path.Combine(uploadRoot, serverFileName));

        await using (var input = file.OpenReadStream(upload.MaxFileSizeBytes, cancellationToken))
        await using (var output = new FileStream(destination, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, FileOptions.Asynchronous))
        {
            await input.CopyToAsync(output, cancellationToken);
        }

        if (!await HasPdfSignatureAsync(destination, cancellationToken))
        {
            File.Delete(destination);
            throw new InvalidInvoiceUploadException("La signature du fichier ne correspond pas à un PDF valide.");
        }

        logger.LogInformation("Stored PDF upload as temporary file {ServerFileName}.", serverFileName);
        return new StoredFile
        {
            SafeDisplayName = safeDisplayName,
            ServerFileName = serverFileName,
            FullPath = destination,
            UploadedAt = DateTimeOffset.UtcNow
        };
    }

    private string ResolveUploadRoot()
    {
        return Path.GetFullPath(temporaryFileOptions.Value.UploadPath);
    }

    private static string EnsurePathInsideRoot(string root, string path)
    {
        var normalizedRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var normalizedPath = Path.GetFullPath(path);

        if (!normalizedPath.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Resolved upload path is outside the configured temporary folder.");
        }

        return normalizedPath;
    }

    private static string SanitizeDisplayName(string fileName)
    {
        var name = Path.GetFileName(fileName);
        var safeChars = name.Select(c => char.IsLetterOrDigit(c) || c is '.' or '-' or '_' ? c : '_').ToArray();
        var sanitized = new string(safeChars).Trim('_');
        return string.IsNullOrWhiteSpace(sanitized) ? "facture.pdf" : sanitized;
    }

    private static async Task<bool> HasPdfSignatureAsync(string path, CancellationToken cancellationToken)
    {
        var buffer = new byte[PdfSignature.Length];
        await using var stream = File.OpenRead(path);
        var read = await stream.ReadAsync(buffer, cancellationToken);
        return read == PdfSignature.Length && buffer.SequenceEqual(PdfSignature);
    }
}
