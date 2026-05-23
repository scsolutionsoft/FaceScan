using FaceScan.Web.Models.Enums;

namespace FaceScan.Web.ViewModels.Teacher;

public class PeriodAttendanceQuickUpdateViewModel
{
    public DateTime Date { get; set; }
    public int ClassroomId { get; set; }
    public int ClassPeriodId { get; set; }
    public int StudentId { get; set; }
    public DateTime CheckDate { get; set; }
    public int CheckClassroomId { get; set; }
    public int CheckClassPeriodId { get; set; }
    public int SelectedClassPeriodId { get; set; }
    public PeriodAttendanceStatus Status { get; set; } = PeriodAttendanceStatus.Present;
    public string? Remark { get; set; }
    public string? EditReason { get; set; }
}
