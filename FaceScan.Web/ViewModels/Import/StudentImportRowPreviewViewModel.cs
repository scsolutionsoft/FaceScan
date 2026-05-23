namespace FaceScan.Web.ViewModels.Import;

public class StudentImportRowPreviewViewModel
{
    public int RowNumber { get; set; }
    public string StudentCode { get; set; } = string.Empty;
    public string Prefix { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Gender { get; set; } = string.Empty;
    public string BirthDate { get; set; } = string.Empty;
    public string AcademicYear { get; set; } = string.Empty;
    public string GradeLevel { get; set; } = string.Empty;
    public string Classroom { get; set; } = string.Empty;
    public string StudentNo { get; set; } = string.Empty;
    public string GuardianName { get; set; } = string.Empty;
    public string GuardianPhone { get; set; } = string.Empty;
    public string NationalId { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
    public bool IsValid { get; set; }
    public string? ErrorMessage { get; set; }
}
