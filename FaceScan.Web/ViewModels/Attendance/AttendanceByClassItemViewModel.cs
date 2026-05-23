namespace FaceScan.Web.ViewModels.Attendance;

public class AttendanceByClassItemViewModel
{
    public string GradeLevel { get; set; } = string.Empty;
    public string Classroom { get; set; } = string.Empty;
    public int PresentCount { get; set; }
    public int LateCount { get; set; }
    public int AbsentCount { get; set; }
    public int PartialCount { get; set; }
    public int TotalCount { get; set; }
}
