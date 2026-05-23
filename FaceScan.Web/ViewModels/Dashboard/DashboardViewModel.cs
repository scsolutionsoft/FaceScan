namespace FaceScan.Web.ViewModels.Dashboard;

public class DashboardViewModel
{
    public DateTime TargetDate { get; set; } = DateTime.Today;
    public int TotalStudents { get; set; }
    public int PresentToday { get; set; }
    public int AbsentToday { get; set; }
    public int LateToday { get; set; }
    public int LeaveToday { get; set; }
    public int TruancyToday { get; set; }
    public int CheckInCount { get; set; }
    public int CheckOutCount { get; set; }
    public int PendingCheckoutCount { get; set; }
    public IReadOnlyList<DashboardGradeSummaryViewModel> GradeSummaries { get; set; } = [];
    public IReadOnlyList<DashboardClassroomSummaryViewModel> ClassroomSummaries { get; set; } = [];
    public IReadOnlyList<DashboardLatestScanViewModel> LatestScans { get; set; } = [];
    public IReadOnlyList<DashboardRealtimeStudentViewModel> RealtimeRows { get; set; } = [];
}
