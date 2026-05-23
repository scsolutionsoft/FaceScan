using FaceScan.Web.ViewModels;

namespace FaceScan.Web.ViewModels.TeacherAttendance;

public class TeacherAttendanceIndexViewModel
{
    public TeacherAttendanceFilterViewModel Filter { get; set; } = new();
    public IReadOnlyList<string> Roles { get; set; } = [];
    public IReadOnlyList<SelectOptionViewModel> Classrooms { get; set; } = [];
    public IReadOnlyList<TeacherAttendanceTransactionRowViewModel> Transactions { get; set; } = [];
}
