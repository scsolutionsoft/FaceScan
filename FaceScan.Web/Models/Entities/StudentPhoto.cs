namespace FaceScan.Web.Models.Entities;

public class StudentPhoto : BaseEntity
{
    public int StudentId { get; set; }
    public string FilePath { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public bool IsPrimary { get; set; }
    public DateTime CapturedAt { get; set; } = DateTime.UtcNow;
    public decimal? QualityScore { get; set; }

    public Student? Student { get; set; }
}
