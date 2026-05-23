using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Threading.Channels;
using FaceScan.Web.Hubs;
using FaceScan.Web.Services.Interfaces;
using FaceScan.Web.ViewModels.Scan;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace FaceScan.Web.Services;

public sealed class QueuedScanVerifyService : BackgroundService, IQueuedScanVerifyService
{
    private const int QueueCapacity = 128;
    private const int StartupRejectThreshold = 36;
    private const int MinAdaptiveRejectThreshold = 18;
    private const int MaxAdaptiveRejectThreshold = 72;
    private const int WorkerCount = 3;
    private static readonly TimeSpan JobTimeout = TimeSpan.FromSeconds(4);
    private static readonly TimeSpan MaxQueueWait = TimeSpan.FromSeconds(12);
    private static readonly TimeSpan DuplicateFrameWindow = TimeSpan.FromMilliseconds(900);
    private static readonly TimeSpan MaxDuplicateFrameWindow = TimeSpan.FromMilliseconds(2200);
    private const int MinAcceptedDimension = 96;
    private const double MinBrightness = 18;
    private const double MaxBrightness = 245;
    private const double MinDetailScore = 7.5;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IHubContext<ScanLiveHub> _scanLiveHub;
    private readonly Channel<QueuedWorkItem> _queue;
    private readonly ConcurrentDictionary<string, QueuedScanVerifyJobResult> _results = new();
    private readonly ConcurrentDictionary<string, DateTimeOffset> _recentFrameFingerprints = new(StringComparer.Ordinal);
    private readonly DateTimeOffset _startedAtUtc = DateTimeOffset.UtcNow;
    private int _pendingCount;
    private long _totalEnqueued;
    private long _totalRejected;
    private long _totalRejectedDuplicate;
    private long _totalRejectedLowQuality;
    private long _totalCompleted;
    private long _totalFailed;
    private long _totalTimeout;
    private long _totalDroppedStale;
    private long _totalProcessTicks;
    private long _totalQueueWaitTicks;
    private int _effectiveRejectThreshold = StartupRejectThreshold;

    public QueuedScanVerifyService(IServiceScopeFactory scopeFactory, IHubContext<ScanLiveHub> scanLiveHub)
    {
        _scopeFactory = scopeFactory;
        _scanLiveHub = scanLiveHub;
        _queue = Channel.CreateBounded<QueuedWorkItem>(new BoundedChannelOptions(QueueCapacity)
        {
            SingleReader = false,
            SingleWriter = false,
            FullMode = BoundedChannelFullMode.DropWrite
        });
    }

    public int PendingCount => Math.Max(0, Volatile.Read(ref _pendingCount));

    public QueuedScanVerifyMetricsSnapshot GetMetricsSnapshot()
    {
        var completed = Math.Max(0, Interlocked.Read(ref _totalCompleted));
        var avgProcessMs = completed > 0
            ? TimeSpan.FromTicks(Math.Max(0, Interlocked.Read(ref _totalProcessTicks))).TotalMilliseconds / completed
            : 0;
        var avgQueueWaitMs = completed > 0
            ? TimeSpan.FromTicks(Math.Max(0, Interlocked.Read(ref _totalQueueWaitTicks))).TotalMilliseconds / completed
            : 0;
        var effectiveThreshold = GetEffectiveRejectThreshold();
        var effectiveDuplicateWindow = GetEffectiveDuplicateFrameWindow(effectiveThreshold, avgQueueWaitMs);
        Volatile.Write(ref _effectiveRejectThreshold, effectiveThreshold);

        return new QueuedScanVerifyMetricsSnapshot
        {
            StartedAtUtc = _startedAtUtc,
            PendingCount = PendingCount,
            TotalEnqueued = Interlocked.Read(ref _totalEnqueued),
            TotalRejected = Interlocked.Read(ref _totalRejected),
            TotalCompleted = completed,
            TotalFailed = Interlocked.Read(ref _totalFailed),
            TotalTimeout = Interlocked.Read(ref _totalTimeout),
            TotalDroppedStale = Interlocked.Read(ref _totalDroppedStale),
            TotalRejectedDuplicate = Interlocked.Read(ref _totalRejectedDuplicate),
            TotalRejectedLowQuality = Interlocked.Read(ref _totalRejectedLowQuality),
            EffectiveRejectThreshold = effectiveThreshold,
            EffectiveDuplicateWindowMs = (int)Math.Round(effectiveDuplicateWindow.TotalMilliseconds),
            AverageProcessMs = Math.Round(avgProcessMs, 2),
            AverageQueueWaitMs = Math.Round(avgQueueWaitMs, 2)
        };
    }

    public bool TryEnqueue(QueuedScanVerifyRequest request, out string jobId, out QueuedScanEnqueueDecision decision)
    {
        jobId = string.Empty;
        decision = new QueuedScanEnqueueDecision
        {
            Accepted = false,
            Code = "invalid",
            Message = "ข้อมูลภาพไม่ถูกต้อง"
        };

        var quality = AnalyzeIngressImage(request.ImageBytes);
        if (quality is null)
        {
            Interlocked.Increment(ref _totalRejected);
            Interlocked.Increment(ref _totalRejectedLowQuality);
            decision = new QueuedScanEnqueueDecision
            {
                Accepted = false,
                Code = "invalid-image",
                Message = "ไม่สามารถอ่านภาพที่ส่งมาได้"
            };
            return false;
        }

        if (quality.Width < MinAcceptedDimension || quality.Height < MinAcceptedDimension)
        {
            Interlocked.Increment(ref _totalRejected);
            Interlocked.Increment(ref _totalRejectedLowQuality);
            decision = new QueuedScanEnqueueDecision
            {
                Accepted = false,
                Code = "low-quality",
                Message = "ภาพมีขนาดเล็กเกินไปสำหรับสแกนให้แม่นยำ",
                Quality = quality,
                FrameFingerprint = quality.FrameFingerprint
            };
            return false;
        }

        if (quality.MeanBrightness < MinBrightness || quality.MeanBrightness > MaxBrightness || quality.DetailScore < MinDetailScore)
        {
            Interlocked.Increment(ref _totalRejected);
            Interlocked.Increment(ref _totalRejectedLowQuality);
            decision = new QueuedScanEnqueueDecision
            {
                Accepted = false,
                Code = "low-quality",
                Message = "ภาพเบลอหรือแสงไม่เหมาะสม ระบบข้ามเฟรมนี้เพื่อรักษาความเร็ว",
                Quality = quality,
                FrameFingerprint = quality.FrameFingerprint
            };
            return false;
        }

        var threshold = GetEffectiveRejectThreshold();
        Volatile.Write(ref _effectiveRejectThreshold, threshold);
        var effectiveDuplicateWindow = GetEffectiveDuplicateFrameWindow(threshold);
        CleanupExpiredFingerprints();
        var now = DateTimeOffset.UtcNow;
        if (_recentFrameFingerprints.TryGetValue(quality.FrameFingerprint, out var seenAtUtc)
            && (now - seenAtUtc) <= effectiveDuplicateWindow)
        {
            Interlocked.Increment(ref _totalRejected);
            Interlocked.Increment(ref _totalRejectedDuplicate);
            decision = new QueuedScanEnqueueDecision
            {
                Accepted = false,
                Code = "duplicate-frame",
                Message = "ข้ามภาพซ้ำที่เพิ่งส่งเข้ามาเพื่อลดคิวสะสม",
                RetryAfterMs = (int)Math.Max(120, (effectiveDuplicateWindow - (now - seenAtUtc)).TotalMilliseconds),
                Quality = quality,
                FrameFingerprint = quality.FrameFingerprint
            };
            return false;
        }

        if (PendingCount >= threshold)
        {
            Interlocked.Increment(ref _totalRejected);
            decision = new QueuedScanEnqueueDecision
            {
                Accepted = false,
                Code = "overloaded",
                Message = "คิวประมวลผลเต็มชั่วคราว",
                RetryAfterMs = Math.Clamp(700 + (PendingCount * 18), 900, 3000),
                Quality = quality,
                FrameFingerprint = quality.FrameFingerprint
            };
            return false;
        }

        jobId = Guid.NewGuid().ToString("N");
        _results[jobId] = new QueuedScanVerifyJobResult
        {
            Status = "pending",
            Message = "queued",
            CreatedAtUtc = DateTimeOffset.UtcNow
        };

        var item = new QueuedWorkItem(jobId, request, DateTimeOffset.UtcNow);
        if (!_queue.Writer.TryWrite(item))
        {
            _results.TryRemove(jobId, out _);
            jobId = string.Empty;
            Interlocked.Increment(ref _totalRejected);
            decision = new QueuedScanEnqueueDecision
            {
                Accepted = false,
                Code = "overloaded",
                Message = "คิวประมวลผลเต็มชั่วคราว",
                RetryAfterMs = Math.Clamp(700 + (PendingCount * 18), 900, 3000),
                Quality = quality,
                FrameFingerprint = quality.FrameFingerprint
            };
            return false;
        }

        Interlocked.Increment(ref _pendingCount);
        Interlocked.Increment(ref _totalEnqueued);
        _recentFrameFingerprints[quality.FrameFingerprint] = now;
        decision = new QueuedScanEnqueueDecision
        {
            Accepted = true,
            Code = "accepted",
            Message = "queued",
            Quality = quality,
            FrameFingerprint = quality.FrameFingerprint
        };
        return true;
    }

    private int GetEffectiveRejectThreshold()
    {
        var completed = Interlocked.Read(ref _totalCompleted);
        if (completed < 3)
        {
            return StartupRejectThreshold;
        }

        var avgTicks = Interlocked.Read(ref _totalProcessTicks) / Math.Max(1, completed);
        if (avgTicks <= 0)
        {
            return StartupRejectThreshold;
        }

        var avgProcessMs = TimeSpan.FromTicks(avgTicks).TotalMilliseconds;
        var avgQueueWaitTicks = Interlocked.Read(ref _totalQueueWaitTicks) / Math.Max(1, completed);
        var avgQueueWaitMs = avgQueueWaitTicks > 0 ? TimeSpan.FromTicks(avgQueueWaitTicks).TotalMilliseconds : 0;

        // Keep queue short when queue wait starts approaching the stale-drop window.
        var processLimitedCapacity = (int)Math.Floor((WorkerCount * MaxQueueWait.TotalMilliseconds) / Math.Max(1, avgProcessMs));
        var waitPressureFactor = avgQueueWaitMs <= 1
            ? 1.0
            : Math.Clamp(1.0 - (avgQueueWaitMs / MaxQueueWait.TotalMilliseconds), 0.35, 1.0);

        var safeCapacity = (int)Math.Floor(processLimitedCapacity * waitPressureFactor);
        return Math.Clamp(safeCapacity, MinAdaptiveRejectThreshold, MaxAdaptiveRejectThreshold);
    }

    private TimeSpan GetEffectiveDuplicateFrameWindow(int? effectiveThreshold = null, double? avgQueueWaitMsOverride = null)
    {
        var threshold = Math.Max(1, effectiveThreshold ?? Volatile.Read(ref _effectiveRejectThreshold));
        var pendingPressure = Math.Clamp(PendingCount / (double)threshold, 0, 1.25);

        double avgQueueWaitMs;
        if (avgQueueWaitMsOverride.HasValue)
        {
            avgQueueWaitMs = Math.Max(0, avgQueueWaitMsOverride.Value);
        }
        else
        {
            var completed = Math.Max(0, Interlocked.Read(ref _totalCompleted));
            avgQueueWaitMs = completed > 0
                ? TimeSpan.FromTicks(Math.Max(0, Interlocked.Read(ref _totalQueueWaitTicks))).TotalMilliseconds / completed
                : 0;
        }

        var waitPressure = Math.Clamp(avgQueueWaitMs / 1500d, 0, 1.0);
        var pressure = Math.Max(pendingPressure, waitPressure);
        var scaledMs = DuplicateFrameWindow.TotalMilliseconds * (1 + (pressure * 1.45));
        return TimeSpan.FromMilliseconds(Math.Clamp(scaledMs, DuplicateFrameWindow.TotalMilliseconds, MaxDuplicateFrameWindow.TotalMilliseconds));
    }

    public bool TryGetResult(string jobId, out QueuedScanVerifyJobResult? result)
    {
        if (_results.TryGetValue(jobId, out var found))
        {
            result = found;
            return true;
        }

        result = null;
        return false;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var workers = new List<Task>(WorkerCount);
        for (var i = 0; i < WorkerCount; i++)
        {
            workers.Add(RunWorkerAsync(stoppingToken));
        }

        await Task.WhenAll(workers);
    }

    private async Task RunWorkerAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            QueuedWorkItem item;
            try
            {
                item = await _queue.Reader.ReadAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            try
            {
                var queueWait = DateTimeOffset.UtcNow - item.EnqueuedAtUtc;
                if (queueWait > MaxQueueWait)
                {
                    _results[item.JobId] = new QueuedScanVerifyJobResult
                    {
                        Status = "failed",
                        Message = "stale",
                        CreatedAtUtc = _results.TryGetValue(item.JobId, out var prev) ? prev.CreatedAtUtc : item.EnqueuedAtUtc,
                        CompletedAtUtc = DateTimeOffset.UtcNow
                    };

                    Interlocked.Increment(ref _totalFailed);
                    Interlocked.Increment(ref _totalDroppedStale);
                    Interlocked.Increment(ref _totalCompleted);
                    Interlocked.Add(ref _totalQueueWaitTicks, queueWait.Ticks);
                    continue;
                }

                await ProcessItemAsync(item, stoppingToken);
                CleanupExpiredResults();
            }
            finally
            {
                Interlocked.Decrement(ref _pendingCount);
            }
        }
    }

    private async Task ProcessItemAsync(QueuedWorkItem item, CancellationToken cancellationToken)
    {
        var sw = Stopwatch.StartNew();
        var queueWait = DateTimeOffset.UtcNow - item.EnqueuedAtUtc;
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var attendanceService = scope.ServiceProvider.GetRequiredService<IAttendanceService>();
            var resolver = scope.ServiceProvider.GetRequiredService<IFaceRecognitionServiceResolver>();

            var request = new ScanVerifyRequestViewModel
            {
                StationCode = item.Request.StationCode,
                StationToken = item.Request.StationToken,
                ClientCapturedAtLocal = item.Request.ClientCapturedAtLocal,
                Latitude = item.Request.Latitude,
                Longitude = item.Request.Longitude,
                LocationAccuracyMeters = item.Request.LocationAccuracyMeters,
                RequestedType = item.Request.RequestedType,
                RecognitionProfile = item.Request.RecognitionProfile,
                IsPublicMode = item.Request.IsPublicMode
            };

            await using var imageStream = new MemoryStream(item.Request.ImageBytes, writable: false);
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(JobTimeout);
            var processResult = await attendanceService.ProcessScanAsync(request, imageStream, timeoutCts.Token);

            var response = new ScanVerifyResponseViewModel
            {
                Success = processResult.Success,
                Message = processResult.Message,
                Provider = processResult.Provider,
                StudentId = processResult.StudentId,
                StudentCode = processResult.StudentCode,
                StudentName = processResult.StudentName,
                GradeLevel = processResult.GradeLevel,
                Classroom = processResult.Classroom,
                ScanType = processResult.ScanType,
                ScanTime = processResult.ScanTime,
                Confidence = processResult.ConfidenceScore,
                RecognitionProfile = string.IsNullOrWhiteSpace(processResult.RecognitionProfile)
                    ? resolver.NormalizeProfile(item.Request.RecognitionProfile)
                    : processResult.RecognitionProfile,
                IsDuplicate = processResult.IsDuplicate,
                TimeSource = processResult.TimeSource,
                ClockAnomaly = processResult.ClockAnomaly,
                ClockSkewMinutes = processResult.ClockSkewMinutes
            };

            _results[item.JobId] = new QueuedScanVerifyJobResult
            {
                Status = "completed",
                Message = "completed",
                Response = response,
                CreatedAtUtc = _results.TryGetValue(item.JobId, out var prev) ? prev.CreatedAtUtc : DateTimeOffset.UtcNow,
                CompletedAtUtc = DateTimeOffset.UtcNow
            };

            Interlocked.Increment(ref _totalCompleted);
            Interlocked.Add(ref _totalProcessTicks, sw.ElapsedTicks);
            Interlocked.Add(ref _totalQueueWaitTicks, Math.Max(0, queueWait.Ticks));

            await PublishLiveEventAsync(item.Request, response, cancellationToken);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            _results[item.JobId] = new QueuedScanVerifyJobResult
            {
                Status = "failed",
                Message = "timeout",
                CreatedAtUtc = _results.TryGetValue(item.JobId, out var prev) ? prev.CreatedAtUtc : DateTimeOffset.UtcNow,
                CompletedAtUtc = DateTimeOffset.UtcNow
            };

            Interlocked.Increment(ref _totalFailed);
            Interlocked.Increment(ref _totalTimeout);
            Interlocked.Increment(ref _totalCompleted);
            Interlocked.Add(ref _totalQueueWaitTicks, Math.Max(0, queueWait.Ticks));
        }
        catch (Exception ex)
        {
            _results[item.JobId] = new QueuedScanVerifyJobResult
            {
                Status = "failed",
                Message = ex.Message,
                CreatedAtUtc = _results.TryGetValue(item.JobId, out var prev) ? prev.CreatedAtUtc : DateTimeOffset.UtcNow,
                CompletedAtUtc = DateTimeOffset.UtcNow
            };

            Interlocked.Increment(ref _totalFailed);
            Interlocked.Increment(ref _totalCompleted);
            Interlocked.Add(ref _totalQueueWaitTicks, Math.Max(0, queueWait.Ticks));
        }
    }

    private void CleanupExpiredResults()
    {
        var threshold = DateTimeOffset.UtcNow.AddMinutes(-15);
        foreach (var kvp in _results)
        {
            var timestamp = kvp.Value.CompletedAtUtc ?? kvp.Value.CreatedAtUtc;
            if (timestamp < threshold)
            {
                _results.TryRemove(kvp.Key, out _);
            }
        }
    }

    private void CleanupExpiredFingerprints()
    {
        var threshold = DateTimeOffset.UtcNow - MaxDuplicateFrameWindow;
        foreach (var kvp in _recentFrameFingerprints)
        {
            if (kvp.Value < threshold)
            {
                _recentFrameFingerprints.TryRemove(kvp.Key, out _);
            }
        }
    }

    private static ScanIngressQualitySnapshot? AnalyzeIngressImage(byte[] imageBytes)
    {
        if (imageBytes.Length == 0)
        {
            return null;
        }

        try
        {
            using var image = Image.Load<Rgb24>(imageBytes);
            var width = image.Width;
            var height = image.Height;

            using var preview = image.Clone(ctx => ctx.Resize(new ResizeOptions
            {
                Mode = ResizeMode.Stretch,
                Sampler = KnownResamplers.Triangle,
                Size = new Size(32, 32)
            }));

            var luminance = new byte[32 * 32];
            var index = 0;
            double brightnessSum = 0;
            double detailAccumulator = 0;

            preview.ProcessPixelRows(accessor =>
            {
                for (var y = 0; y < accessor.Height; y++)
                {
                    var row = accessor.GetRowSpan(y);
                    for (var x = 0; x < row.Length; x++)
                    {
                        var pixel = row[x];
                        var gray = (byte)Math.Clamp((0.299 * pixel.R) + (0.587 * pixel.G) + (0.114 * pixel.B), 0, 255);
                        luminance[index++] = gray;
                        brightnessSum += gray;

                        if (x > 0)
                        {
                            detailAccumulator += Math.Abs(gray - luminance[(y * preview.Width) + x - 1]);
                        }

                        if (y > 0)
                        {
                            detailAccumulator += Math.Abs(gray - luminance[((y - 1) * preview.Width) + x]);
                        }
                    }
                }
            });

            var meanBrightness = brightnessSum / luminance.Length;
            var detailScore = detailAccumulator / ((preview.Width - 1) * preview.Height + (preview.Height - 1) * preview.Width);
            var fingerprint = ComputeFrameFingerprint(luminance, meanBrightness);

            return new ScanIngressQualitySnapshot
            {
                Width = width,
                Height = height,
                MeanBrightness = Math.Round(meanBrightness, 2),
                DetailScore = Math.Round(detailScore, 2),
                FrameFingerprint = fingerprint
            };
        }
        catch
        {
            return null;
        }
    }

    private static string ComputeFrameFingerprint(ReadOnlySpan<byte> luminance, double meanBrightness)
    {
        Span<byte> compact = stackalloc byte[16];
        var segmentSize = Math.Max(1, luminance.Length / compact.Length);
        for (var i = 0; i < compact.Length; i++)
        {
            var start = i * segmentSize;
            var end = Math.Min(luminance.Length, start + segmentSize);
            if (start >= end)
            {
                compact[i] = 0;
                continue;
            }

            var sum = 0;
            for (var j = start; j < end; j++)
            {
                sum += luminance[j];
            }

            compact[i] = (byte)(sum / (end - start));
        }

        var fingerprintBytes = SHA256.HashData(compact);
        return Convert.ToHexString(fingerprintBytes[..8]) + ":" + Math.Round(meanBrightness, 1).ToString("0.0", CultureInfo.InvariantCulture);
    }

    private Task PublishLiveEventAsync(QueuedScanVerifyRequest request, ScanVerifyResponseViewModel response, CancellationToken cancellationToken)
    {
        var eventType = !response.Success
            ? "failed"
            : response.IsDuplicate
                ? "duplicate"
                : "success";

        var payload = new
        {
            scope = "student",
            eventType,
            success = response.Success,
            isDuplicate = response.IsDuplicate,
            message = response.Message,
            scanType = response.ScanType?.ToString(),
            scanTime = response.ScanTime,
            code = response.StudentCode,
            name = response.StudentName,
            gradeLevel = response.GradeLevel,
            classroom = response.Classroom,
            confidence = response.Confidence,
            provider = response.Provider,
            recognitionProfile = response.RecognitionProfile,
            stationCode = request.IsPublicMode ? "PUBLIC" : request.StationCode,
            isPublicMode = request.IsPublicMode
        };

        return _scanLiveHub.Clients
            .Group(ScanLiveHub.ExecutiveGroup)
            .SendAsync(ScanLiveHub.ScanEventMethod, payload, cancellationToken);
    }

    private sealed record QueuedWorkItem(string JobId, QueuedScanVerifyRequest Request, DateTimeOffset EnqueuedAtUtc);
}
