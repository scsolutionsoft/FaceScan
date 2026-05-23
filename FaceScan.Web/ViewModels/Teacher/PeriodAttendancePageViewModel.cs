using FaceScan.Web.Models.Enums;
using FaceScan.Web.ViewModels;

namespace FaceScan.Web.ViewModels.Teacher;

public class PeriodAttendancePageViewModel
{
    public PeriodAttendanceFilterViewModel Filter { get; set; } = new();
    public PeriodAttendanceFilterViewModel ReportFilter { get; set; } = new();
    public IReadOnlyList<SelectOptionViewModel> ClassroomOptions { get; set; } = [];
    public IReadOnlyList<SelectOptionViewModel> PeriodOptions { get; set; } = [];
    public IReadOnlyList<PeriodAttendanceStudentItemViewModel> Students { get; set; } = [];
    public IReadOnlyList<PeriodAttendanceReportRowViewModel> ReportRows { get; set; } = [];
    public bool HasClassroomPermission { get; set; } = true;
    public bool IsClassroomLocked { get; set; }
    public bool HasSavedData { get; set; }
    public string? LastUpdatedByName { get; set; }
    public DateTime? LastUpdatedAt { get; set; }
    public TeacherTeachingStatus TeacherStatus { get; set; } = TeacherTeachingStatus.Normal;
    public string? TeacherStatusNote { get; set; }
    public IReadOnlyList<PeriodAttendanceEditHistoryItemViewModel> EditHistory { get; set; } = [];
}
