using FaceScan.Web.Models.Enums;

namespace FaceScan.Web.ViewModels.Teacher;

public class PeriodAttendanceSaveStudentViewModel
{
    public int StudentId { get; set; }
    public PeriodAttendanceStatus Status { get; set; } = PeriodAttendanceStatus.Present;
    public string? Remark { get; set; }
}
