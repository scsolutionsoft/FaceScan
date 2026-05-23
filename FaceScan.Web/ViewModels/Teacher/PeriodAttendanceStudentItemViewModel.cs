using FaceScan.Web.Models.Enums;

namespace FaceScan.Web.ViewModels.Teacher;

public class PeriodAttendanceStudentItemViewModel
{
    public int StudentId { get; set; }
    public string StudentCode { get; set; } = string.Empty;
    public string StudentName { get; set; } = string.Empty;
    public string GradeLevel { get; set; } = string.Empty;
    public string Classroom { get; set; } = string.Empty;
    public string? PhotoPath { get; set; }
    public PeriodAttendanceStatus Status { get; set; } = PeriodAttendanceStatus.Present;
    public string? Remark { get; set; }
}
