namespace FaceScan.Web.Models.Entities;

public class TeacherFacePhoto : BaseEntity
{
    public string UserId { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public bool IsPrimary { get; set; }
    public DateTime CapturedAt { get; set; } = DateTime.UtcNow;
    public decimal? QualityScore { get; set; }

    public ApplicationUser? User { get; set; }
}
