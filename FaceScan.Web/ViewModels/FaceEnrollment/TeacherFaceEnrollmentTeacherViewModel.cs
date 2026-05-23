using FaceScan.Web.Models.Enums;

namespace FaceScan.Web.ViewModels.FaceEnrollment;

public class TeacherFaceEnrollmentTeacherViewModel
{
    public string TeacherId { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string RoleName { get; set; } = string.Empty;
    public string AssignedClassroomName { get; set; } = string.Empty;
    public EnrollmentStatus EnrollmentStatus { get; set; }
    public int PhotoCount { get; set; }
}
