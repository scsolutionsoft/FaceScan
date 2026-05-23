using FaceScan.Web.Models.Enums;

namespace FaceScan.Web.ViewModels.FaceEnrollment;

public class FaceEnrollmentStudentViewModel
{
    public int StudentId { get; set; }
    public string StudentCode { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string ClassroomName { get; set; } = string.Empty;
    public EnrollmentStatus EnrollmentStatus { get; set; }
    public int PhotoCount { get; set; }
}
