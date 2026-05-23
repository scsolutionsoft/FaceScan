using FaceScan.Web.Models.Enums;

namespace FaceScan.Web.ViewModels.Students;

public class StudentListItemViewModel
{
    public int Id { get; set; }
    public string StudentCode { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string GradeLevelName { get; set; } = string.Empty;
    public string ClassroomName { get; set; } = string.Empty;
    public string AcademicYearName { get; set; } = string.Empty;
    public string? StudentNo { get; set; }
    public bool IsActive { get; set; }
    public EnrollmentStatus EnrollmentStatus { get; set; }
}
