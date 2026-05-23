using FaceScan.Web.Models.Options;
using FaceScan.Web.Services.Interfaces;
using Microsoft.Extensions.Options;

namespace FaceScan.Web.Services;

public class FaceRecognitionLoadGate : IFaceRecognitionLoadGate
{
    private readonly SemaphoreSlim _semaphore;

    public FaceRecognitionLoadGate(IOptions<FaceRecognitionOptions> options)
    {
        var configuredMax = options.Value.MaxConcurrentDlibWorkers;
        MaxConcurrency = configuredMax > 0
            ? configuredMax
            : Math.Max(1, Environment.ProcessorCount / 2);

        _semaphore = new SemaphoreSlim(MaxConcurrency, MaxConcurrency);
    }

    public int MaxConcurrency { get; }

    public async Task<IDisposable> EnterAsync(CancellationToken cancellationToken = default)
    {
        await _semaphore.WaitAsync(cancellationToken);
        return new GateLease(_semaphore);
    }

    private sealed class GateLease : IDisposable
    {
        private readonly SemaphoreSlim _semaphore;
        private bool _disposed;

        public GateLease(SemaphoreSlim semaphore)
        {
            _semaphore = semaphore;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _semaphore.Release();
        }
    }
}
