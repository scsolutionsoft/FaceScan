namespace FaceScan.Web.ViewModels.StudentPortal;

public class StudentAttendanceReportViewModel
{
    public string StudentCode { get; set; } = string.Empty;
    public string StudentName { get; set; } = string.Empty;
    public string GradeLevel { get; set; } = string.Empty;
    public string Classroom { get; set; } = string.Empty;
    public TimeSpan LateAfterTime { get; set; }
    public StudentAttendanceReportFilterViewModel Filter { get; set; } = new();
    public IReadOnlyList<StudentAttendanceReportRowViewModel> Rows { get; set; } = [];
    public int PresentCount { get; set; }
    public int LateCount { get; set; }
    public int AbsentCount { get; set; }
    public int PendingCheckoutCount { get; set; }
}
