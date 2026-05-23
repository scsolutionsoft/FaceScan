using System.ComponentModel.DataAnnotations;

namespace FaceScan.Web.ViewModels.EdgeAgent;

public class EdgeAgentHeartbeatRequestViewModel
{
    [Required]
    [MaxLength(50)]
    public string StationCode { get; set; } = string.Empty;

    [Required]
    [MaxLength(120)]
    public string StationToken { get; set; } = string.Empty;

    [Required]
    [MaxLength(120)]
    public string AgentId { get; set; } = string.Empty;

    [MaxLength(300)]
    public string? Message { get; set; }

    public DateTime? CapturedAtUtc { get; set; }
}