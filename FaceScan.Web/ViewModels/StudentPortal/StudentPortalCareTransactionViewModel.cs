namespace FaceScan.Web.ViewModels.StudentPortal;

public class StudentPortalCareTransactionViewModel
{
    public DateTime RecordedAt { get; set; }
    public string Category { get; set; } = string.Empty;
    public int Value { get; set; }
    public string? Description { get; set; }
}
