namespace FaceScan.Web.Models.Entities;

public class ErrorLog : BaseEntity
{
    public string Message { get; set; } = string.Empty;
    public string? StackTrace { get; set; }
    public DateTime LoggedAt { get; set; } = DateTime.UtcNow;
    public string? Source { get; set; }
}
