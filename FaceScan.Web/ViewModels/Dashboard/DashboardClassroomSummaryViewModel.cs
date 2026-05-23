namespace FaceScan.Web.ViewModels.Dashboard;

public class DashboardClassroomSummaryViewModel
{
    public string GradeLevel { get; set; } = string.Empty;
    public string Classroom { get; set; } = string.Empty;
    public int TotalStudents { get; set; }
    public int PresentStudents { get; set; }
    public int AbsentStudents { get; set; }
}
