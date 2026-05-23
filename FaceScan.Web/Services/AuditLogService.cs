using FaceScan.Web.Data;
using FaceScan.Web.Models.Entities;
using FaceScan.Web.Services.Interfaces;

namespace FaceScan.Web.Services;

public class AuditLogService : IAuditLogService
{
    private readonly ApplicationDbContext _dbContext;

    public AuditLogService(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task LogAsync(
        string? userId,
        string action,
        string entityName,
        string entityId,
        string detail,
        string? ipAddress,
        CancellationToken cancellationToken = default)
    {
        var log = new AuditLog
        {
            UserId = userId,
            Action = action,
            EntityName = entityName,
            EntityId = entityId,
            Detail = detail,
            IpAddress = ipAddress,
            LoggedAt = DateTime.UtcNow
        };

        _dbContext.AuditLogs.Add(log);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
