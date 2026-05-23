namespace FaceScan.Web.ViewModels.Teacher;

public class PeriodAttendanceFilterViewModel
{
    public DateTime Date { get; set; } = DateTime.Today;
    public int? ClassroomId { get; set; }
    public int? ClassPeriodId { get; set; }
    public DateTime? ReportDate { get; set; }
    public int? ReportClassroomId { get; set; }
    public int? ReportClassPeriodId { get; set; }
}
