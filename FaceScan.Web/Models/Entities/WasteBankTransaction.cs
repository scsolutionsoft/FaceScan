namespace FaceScan.Web.Models.Entities;

public class WasteBankTransaction : BaseEntity
{
    public int StudentId { get; set; }
    public int AcademicYearId { get; set; }
    public string WasteType { get; set; } = string.Empty;
    public decimal WeightKg { get; set; }
    public decimal PricePerKg { get; set; }
    public decimal Amount { get; set; }
    public string? RecordedByUserId { get; set; }
    public DateTime RecordedAt { get; set; } = DateTime.UtcNow;
    public string? Note { get; set; }

    public Student? Student { get; set; }
    public AcademicYear? AcademicYear { get; set; }
    public ApplicationUser? RecordedByUser { get; set; }
}
