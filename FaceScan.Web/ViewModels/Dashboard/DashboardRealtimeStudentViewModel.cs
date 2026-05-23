namespace FaceScan.Web.ViewModels.Dashboard;

public class DashboardRealtimeStudentViewModel
{
    public int StudentId { get; set; }
    public string StudentCode { get; set; } = string.Empty;
    public string StudentName { get; set; } = string.Empty;
    public string Classroom { get; set; } = string.Empty;
    public string? PhotoPath { get; set; }
    public DateTime? FirstCheckInTime { get; set; }
    public DateTime? LastCheckOutTime { get; set; }
    public DateTime? LatestScanTime { get; set; }
}
