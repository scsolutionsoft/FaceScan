using FaceScan.Web.ViewModels;

namespace FaceScan.Web.ViewModels.Executive;

public class ExecutiveDashboardViewModel
{
    public ExecutiveReportFilterViewModel Filter { get; set; } = new();
    public IReadOnlyList<SelectOptionViewModel> GradeLevels { get; set; } = [];
    public IReadOnlyList<SelectOptionViewModel> Classrooms { get; set; } = [];
    public IReadOnlyList<ExecutiveStudentReportRowViewModel> Rows { get; set; } = [];
    public IReadOnlyList<ExecutiveDailySummaryViewModel> DailySummaries { get; set; } = [];
    public IReadOnlyList<ExecutiveAttendanceGroupSummaryViewModel> GradeSummaries { get; set; } = [];
    public IReadOnlyList<ExecutiveAttendanceGroupSummaryViewModel> ClassroomSummaries { get; set; } = [];
    public IReadOnlyList<ExecutiveAttendanceGroupSummaryViewModel> TopRiskGrades { get; set; } = [];
    public IReadOnlyList<ExecutiveAttendanceGroupSummaryViewModel> TopRiskClassrooms { get; set; } = [];
    public IReadOnlyList<ExecutiveRiskStudentViewModel> TopRiskStudents { get; set; } = [];
    public IReadOnlyList<ExecutiveRiskTrendPointViewModel> RiskTrendPoints { get; set; } = [];
    public IReadOnlyList<ExecutiveAttendanceInsightViewModel> Insights { get; set; } = [];
    public int TotalStudents { get; set; }
    public int RegisteredFaceCount { get; set; }
    public int PresentCount { get; set; }
    public int AbsentCount { get; set; }
    public int LeaveCount { get; set; }
    public int LateCount { get; set; }
    public int TruancyCount { get; set; }
    public int OtherCount { get; set; }
}
