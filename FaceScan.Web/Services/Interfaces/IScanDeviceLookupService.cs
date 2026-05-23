namespace FaceScan.Web.Services.Interfaces;

public interface IScanDeviceLookupService
{
    Task<bool> IsValidActiveDeviceAsync(string? stationCode, string? stationToken, CancellationToken cancellationToken = default);
    Task<int?> GetActiveDeviceIdAsync(string? stationCode, string? stationToken, CancellationToken cancellationToken = default);
    Task TouchLastSeenAsync(int deviceId, CancellationToken cancellationToken = default);
}
