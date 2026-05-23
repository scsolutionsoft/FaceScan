using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;

namespace FaceScan.Web.Services;

public sealed class DebugSnapshotRetentionService : BackgroundService
{
    private static readonly TimeSpan SweepInterval = TimeSpan.FromHours(6);
    private static readonly TimeSpan MaxAge = TimeSpan.FromDays(14);
    private readonly IWebHostEnvironment _webHostEnvironment;

    public DebugSnapshotRetentionService(IWebHostEnvironment webHostEnvironment)
    {
        _webHostEnvironment = webHostEnvironment;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                CleanupExpiredSnapshots();
            }
            catch
            {
                // Ignore one-off cleanup errors and retry on next cycle.
            }

            try
            {
                await Task.Delay(SweepInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private void CleanupExpiredSnapshots()
    {
        var webRoot = _webHostEnvironment.WebRootPath;
        if (string.IsNullOrWhiteSpace(webRoot))
        {
            return;
        }

        var root = Path.Combine(webRoot, "uploads", "ipcamera-debug");
        if (!Directory.Exists(root))
        {
            return;
        }

        var thresholdUtc = DateTime.UtcNow.Subtract(MaxAge);
        var stationDirs = Directory.EnumerateDirectories(root);
        foreach (var stationDir in stationDirs)
        {
            foreach (var filePath in Directory.EnumerateFiles(stationDir, "critical-*.*", SearchOption.TopDirectoryOnly))
            {
                try
                {
                    var info = new FileInfo(filePath);
                    if (info.CreationTimeUtc < thresholdUtc)
                    {
                        File.Delete(filePath);
                    }
                }
                catch
                {
                    // Keep going for other files.
                }
            }

            try
            {
                if (!Directory.EnumerateFileSystemEntries(stationDir).Any())
                {
                    Directory.Delete(stationDir, false);
                }
            }
            catch
            {
                // Keep directory if delete fails.
            }
        }
    }
}
