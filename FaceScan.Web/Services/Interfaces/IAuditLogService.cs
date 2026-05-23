namespace FaceScan.Web.Services.Interfaces;

public interface IAuditLogService
{
    Task LogAsync(string? userId, string action, string entityName, string entityId, string detail, string? ipAddress, CancellationToken cancellationToken = default);
}
