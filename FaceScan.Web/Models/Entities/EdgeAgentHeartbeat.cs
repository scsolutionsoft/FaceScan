namespace FaceScan.Web.Models.Entities;

public class EdgeAgentHeartbeat : BaseEntity
{
    public string StationCode { get; set; } = string.Empty;
    public string AgentId { get; set; } = string.Empty;
    public DateTime LastSeenAtUtc { get; set; } = DateTime.UtcNow;
    public string? LastMessage { get; set; }
    public string? LastIpAddress { get; set; }
}