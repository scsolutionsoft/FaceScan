namespace FaceScan.Web.Models.Entities;

public class AuditLog : BaseEntity
{
    public string? UserId { get; set; }
    public string Action { get; set; } = string.Empty;
    public string EntityName { get; set; } = string.Empty;
    public string EntityId { get; set; } = string.Empty;
    public string Detail { get; set; } = string.Empty;
    public DateTime LoggedAt { get; set; } = DateTime.UtcNow;
    public string? IpAddress { get; set; }

    public ApplicationUser? User { get; set; }
}
