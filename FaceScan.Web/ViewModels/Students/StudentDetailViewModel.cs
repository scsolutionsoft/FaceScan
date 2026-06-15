using FaceScan.Web.Models.Enums;

namespace FaceScan.Web.ViewModels.Students;

public class StudentDetailViewModel
{
    public int Id { get; set; }
    public string StudentCode { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string? NationalId { get; set; }
    public GenderType Gender { get; set; }
    public DateTime? BirthDate { get; set; }
    public string AcademicYearName { get; set; } = string.Empty;
    public string GradeLevelName { get; set; } = string.Empty;
    public string ClassroomName { get; set; } = string.Empty;
    public string? StudentNo { get; set; }
    public string? GuardianName { get; set; }
    public string? GuardianNationalId { get; set; }
    public string? GuardianPhone { get; set; }
    public string? Address { get; set; }
    public string? Notes { get; set; }
    public bool IsActive { get; set; }
    public EnrollmentStatus EnrollmentStatus { get; set; }
    public IReadOnlyList<StudentPhotoViewModel> Photos { get; set; } = [];
}
