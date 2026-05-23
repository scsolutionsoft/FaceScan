using FaceScan.Web.Models.Enums;

namespace FaceScan.Web.ViewModels.StudentPortal;

public class StudentPortalIndexViewModel
{
    public string StudentCode { get; set; } = string.Empty;
    public string StudentName { get; set; } = string.Empty;
    public string GradeLevel { get; set; } = string.Empty;
    public string Classroom { get; set; } = string.Empty;
    public EnrollmentStatus EnrollmentStatus { get; set; }
    public int PhotoCount { get; set; }
    public DateTime? TodayCheckIn { get; set; }
    public DateTime? TodayCheckOut { get; set; }
    public bool IsLateToday { get; set; }
    public TimeSpan LateAfterTime { get; set; }
}
