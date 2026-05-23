using FaceScan.Web.Data;
using FaceScan.Web.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace FaceScan.Web.Services;

public class ScanDeviceLookupService : IScanDeviceLookupService
{
    private static readonly TimeSpan DeviceCacheAbsolute = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan DeviceCacheSliding = TimeSpan.FromMinutes(3);
    private static readonly TimeSpan DeviceTouchThrottle = TimeSpan.FromMinutes(5);

    private readonly ApplicationDbContext _dbContext;
    private readonly IMemoryCache _memoryCache;

    public ScanDeviceLookupService(ApplicationDbContext dbContext, IMemoryCache memoryCache)
    {
        _dbContext = dbContext;
        _memoryCache = memoryCache;
    }

    public async Task<bool> IsValidActiveDeviceAsync(
        string? stationCode,
        string? stationToken,
        CancellationToken cancellationToken = default)
    {
        return (await GetActiveDeviceIdAsync(stationCode, stationToken, cancellationToken)).HasValue;
    }

    public async Task<int?> GetActiveDeviceIdAsync(
        string? stationCode,
        string? stationToken,
        CancellationToken cancellationToken = default)
    {
        var cacheKey = BuildCacheKey(stationCode, stationToken);
        if (cacheKey is null)
        {
            return null;
        }

        if (_memoryCache.TryGetValue<int?>(cacheKey, out var cachedId))
        {
            return cachedId;
        }

        var deviceId = await _dbContext.ScanDevices
            .AsNoTracking()
            .Where(x => x.StationCode == stationCode && x.AccessToken == stationToken && x.IsActive)
            .Select(x => (int?)x.Id)
            .FirstOrDefaultAsync(cancellationToken);

        _memoryCache.Set(
            cacheKey,
            deviceId,
            new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = DeviceCacheAbsolute,
                SlidingExpiration = DeviceCacheSliding
            });

        return deviceId;
    }

    public async Task TouchLastSeenAsync(int deviceId, CancellationToken cancellationToken = default)
    {
        if (deviceId <= 0)
        {
            return;
        }

        var touchCacheKey = $"scan-device-touch:{deviceId}";
        if (_memoryCache.TryGetValue(touchCacheKey, out _))
        {
            return;
        }

        _memoryCache.Set(
            touchCacheKey,
            true,
            new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = DeviceTouchThrottle
            });

        var now = DateTime.UtcNow;
        await _dbContext.ScanDevices
            .Where(x => x.Id == deviceId && x.IsActive)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(x => x.LastSeenAt, _ => now), cancellationToken);
    }

    private static string? BuildCacheKey(string? stationCode, string? stationToken)
    {
        if (string.IsNullOrWhiteSpace(stationCode) || string.IsNullOrWhiteSpace(stationToken))
        {
            return null;
        }

        return $"scan-device:{stationCode.Trim().ToLowerInvariant()}:{stationToken.Trim()}";
    }
}
