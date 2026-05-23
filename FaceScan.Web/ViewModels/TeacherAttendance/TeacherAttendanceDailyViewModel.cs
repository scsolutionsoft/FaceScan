using FaceScan.Web.ViewModels;

namespace FaceScan.Web.ViewModels.TeacherAttendance;

public class TeacherAttendanceDailyViewModel
{
    public TeacherAttendanceFilterViewModel Filter { get; set; } = new();
    public TimeSpan LateAfterTime { get; set; }
    public IReadOnlyList<string> Roles { get; set; } = [];
    public IReadOnlyList<SelectOptionViewModel> Classrooms { get; set; } = [];
    public IReadOnlyList<TeacherAttendanceDailyRowViewModel> Rows { get; set; } = [];
}
