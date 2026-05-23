using FaceScan.Web.ViewModels;

namespace FaceScan.Web.ViewModels.Attendance;

public class AttendanceByClassViewModel
{
    public AttendanceFilterViewModel Filter { get; set; } = new();
    public IReadOnlyList<SelectOptionViewModel> AcademicYears { get; set; } = [];
    public IReadOnlyList<SelectOptionViewModel> GradeLevels { get; set; } = [];
    public IReadOnlyList<SelectOptionViewModel> Classrooms { get; set; } = [];
    public IReadOnlyList<AttendanceByClassItemViewModel> Items { get; set; } = [];
}
