namespace FaceScan.Web.ViewModels.Attendance;

public class AttendanceFilterViewModel
{
    public DateTime? Date { get; set; }
    public int? AcademicYearId { get; set; }
    public int? GradeLevelId { get; set; }
    public int? ClassroomId { get; set; }
    public string? StudentKeyword { get; set; }
    public string? Status { get; set; }
    public bool? IsLateOnly { get; set; }
    public string SortBy { get; set; } = "status";
}
