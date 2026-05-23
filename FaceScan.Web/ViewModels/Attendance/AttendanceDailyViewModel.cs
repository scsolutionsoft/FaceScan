using FaceScan.Web.ViewModels;

namespace FaceScan.Web.ViewModels.Attendance;

public class AttendanceDailyViewModel
{
    public AttendanceFilterViewModel Filter { get; set; } = new();
    public TimeSpan LateAfterTime { get; set; }
    public IReadOnlyList<SelectOptionViewModel> AcademicYears { get; set; } = [];
    public IReadOnlyList<SelectOptionViewModel> GradeLevels { get; set; } = [];
    public IReadOnlyList<SelectOptionViewModel> Classrooms { get; set; } = [];
    public IReadOnlyList<AttendanceDailyRowViewModel> Rows { get; set; } = [];
}
