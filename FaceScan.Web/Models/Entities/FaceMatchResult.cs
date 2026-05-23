namespace FaceScan.Web.Models.Entities;

public class FaceMatchResult
{
    public bool Success { get; set; }
    public int? StudentId { get; set; }
    public string? StudentCode { get; set; }
    public string? StudentName { get; set; }
    public string? UserId { get; set; }
    public string? UserName { get; set; }
    public string? FullName { get; set; }
    public decimal ConfidenceScore { get; set; }
    public string Provider { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? RawResponseJson { get; set; }
}
