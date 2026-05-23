using FaceScan.Web.Models.Enums;
using FaceScan.Web.ViewModels.Scan;

namespace FaceScan.Web.Services.Interfaces;

public interface IQueuedScanVerifyService
{
    bool TryEnqueue(QueuedScanVerifyRequest request, out string jobId, out QueuedScanEnqueueDecision decision);
    bool TryGetResult(string jobId, out QueuedScanVerifyJobResult? result);
    int PendingCount { get; }
    QueuedScanVerifyMetricsSnapshot GetMetricsSnapshot();
}

public sealed class QueuedScanEnqueueDecision
{
    public bool Accepted { get; init; }
    public string Code { get; init; } = "accepted";
    public string Message { get; init; } = string.Empty;
    public int RetryAfterMs { get; init; }
    public string? FrameFingerprint { get; init; }
    public ScanIngressQualitySnapshot? Quality { get; init; }
}

public sealed class QueuedScanVerifyMetricsSnapshot
{
    public DateTimeOffset StartedAtUtc { get; init; }
    public int PendingCount { get; init; }
    public long TotalEnqueued { get; init; }
    public long TotalRejected { get; init; }
    public long TotalCompleted { get; init; }
    public long TotalFailed { get; init; }
    public long TotalTimeout { get; init; }
    public long TotalDroppedStale { get; init; }
    public long TotalRejectedDuplicate { get; init; }
    public long TotalRejectedLowQuality { get; init; }
    public int EffectiveRejectThreshold { get; init; }
    public int EffectiveDuplicateWindowMs { get; init; }
    public double AverageProcessMs { get; init; }
    public double AverageQueueWaitMs { get; init; }
}

public sealed class QueuedScanVerifyRequest
{
    public byte[] ImageBytes { get; init; } = Array.Empty<byte>();
    public string? StationCode { get; init; }
    public string? StationToken { get; init; }
    public string? ClientCapturedAtLocal { get; init; }
    public decimal? Latitude { get; init; }
    public decimal? Longitude { get; init; }
    public decimal? LocationAccuracyMeters { get; init; }
    public ScanType? RequestedType { get; init; }
    public string? RecognitionProfile { get; init; }
    public bool IsPublicMode { get; init; }
}

public sealed class ScanIngressQualitySnapshot
{
    public int Width { get; init; }
    public int Height { get; init; }
    public double MeanBrightness { get; init; }
    public double DetailScore { get; init; }
    public string FrameFingerprint { get; init; } = string.Empty;
}

public sealed class QueuedScanVerifyJobResult
{
    public string Status { get; init; } = "pending";
    public string Message { get; init; } = string.Empty;
    public ScanVerifyResponseViewModel? Response { get; init; }
    public DateTimeOffset CreatedAtUtc { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? CompletedAtUtc { get; init; }
}
