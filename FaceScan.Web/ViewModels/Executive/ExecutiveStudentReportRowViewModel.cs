namespace FaceScan.Web.ViewModels.Executive;

public class ExecutiveStudentReportRowViewModel
{
    public int StudentId { get; set; }
    public string StudentCode { get; set; } = string.Empty;
    public string StudentName { get; set; } = string.Empty;
    public string GradeLevel { get; set; } = string.Empty;
    public string Classroom { get; set; } = string.Empty;
    public string? PhotoPath { get; set; }
    public DateTime? FirstCheckInTime { get; set; }
    public DateTime? LastCheckOutTime { get; set; }
    public int PresentPeriods { get; set; }
    public int AbsentPeriods { get; set; }
    public int LeavePeriods { get; set; }
    public int LatePeriods { get; set; }
    public int TruancyPeriods { get; set; }
    public int OtherPeriods { get; set; }
}
