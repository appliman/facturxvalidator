using FacturXValidator.Models;
using Microsoft.Extensions.Options;

namespace FacturXValidator.Services;

public sealed class TemporaryFileCleanupService(
    IOptions<TemporaryFileOptions> options,
    ILogger<TemporaryFileCleanupService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                Cleanup();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Temporary file cleanup failed.");
            }

            var interval = TimeSpan.FromMinutes(Math.Max(1, options.Value.CleanupIntervalMinutes));
            await Task.Delay(interval, stoppingToken);
        }
    }

    private void Cleanup()
    {
        var root = Path.GetFullPath(options.Value.UploadPath);
        Directory.CreateDirectory(root);

        var normalizedRoot = root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var cutoff = DateTimeOffset.UtcNow.AddHours(-Math.Max(1, options.Value.RetentionHours));

        foreach (var file in Directory.EnumerateFiles(root, "*", SearchOption.TopDirectoryOnly))
        {
            var fullPath = Path.GetFullPath(file);
            if (!fullPath.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase))
            {
                logger.LogWarning("Skipping cleanup candidate outside configured upload folder.");
                continue;
            }

            var lastWrite = File.GetLastWriteTimeUtc(fullPath);
            if (lastWrite > cutoff.UtcDateTime)
            {
                continue;
            }

            try
            {
                File.Delete(fullPath);
                logger.LogInformation("Deleted expired temporary upload {FileName}.", Path.GetFileName(fullPath));
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to delete expired temporary upload {FileName}.", Path.GetFileName(fullPath));
            }
        }
    }
}
