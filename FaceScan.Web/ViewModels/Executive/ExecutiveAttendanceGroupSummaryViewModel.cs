namespace FaceScan.Web.ViewModels.Executive;

public class ExecutiveAttendanceGroupSummaryViewModel
{
    public int? GradeLevelId { get; set; }
    public int? ClassroomId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string SecondaryLabel { get; set; } = string.Empty;
    public int StudentCount { get; set; }
    public int RegisteredFaceCount { get; set; }
    public int ActiveAttendanceCount { get; set; }
    public int InactiveAttendanceCount { get; set; }
    public int LeaveCount { get; set; }
    public int LateCount { get; set; }
    public int TruancyCount { get; set; }
    public int OtherCount { get; set; }
    public decimal AttendanceRate { get; set; }
    public decimal FaceRegistrationRate { get; set; }
    public DateTime? FirstActivityTime { get; set; }
    public DateTime? LastActivityTime { get; set; }
}