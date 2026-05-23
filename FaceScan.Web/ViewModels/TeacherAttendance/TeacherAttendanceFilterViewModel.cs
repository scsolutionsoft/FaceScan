namespace FaceScan.Web.ViewModels.TeacherAttendance;

public class TeacherAttendanceFilterViewModel
{
    public DateTime? Date { get; set; }
    public int? AssignedClassroomId { get; set; }
    public string? RoleName { get; set; }
    public string? TeacherKeyword { get; set; }
    public string? Status { get; set; }
    public string? SortBy { get; set; }
    public bool? IsLateOnly { get; set; }
}
