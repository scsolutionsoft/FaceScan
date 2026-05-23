using FaceScan.Web.Models.Enums;

namespace FaceScan.Web.ViewModels.Teacher;

public class PeriodAttendanceSaveViewModel
{
    public DateTime Date { get; set; }
    public int ClassroomId { get; set; }
    public int ClassPeriodId { get; set; }
    public DateTime? ReportDate { get; set; }
    public int? ReportClassroomId { get; set; }
    public int? ReportClassPeriodId { get; set; }
    public TeacherTeachingStatus TeacherStatus { get; set; } = TeacherTeachingStatus.Normal;
    public string? TeacherStatusNote { get; set; }
    public string? EditReason { get; set; }
    public List<PeriodAttendanceSaveStudentViewModel> Students { get; set; } = [];
}
