namespace FaceScan.Web.ViewModels.Executive;

public class ExecutiveStudentCareDashboardViewModel
{
    public ExecutiveStudentCareFilterViewModel Filter { get; set; } = new();
    public List<SelectOptionViewModel> AcademicYears { get; set; } = [];
    public List<SelectOptionViewModel> GradeLevels { get; set; } = [];
    public List<SelectOptionViewModel> Classrooms { get; set; } = [];
    public int TotalStudents { get; set; }
    public int ProfileCount { get; set; }
    public int HomeVisitedCount { get; set; }
    public int RiskStudentCount { get; set; }
    public int PendingApprovalCount { get; set; }
    public decimal WasteBankTotalWeightKg { get; set; }
    public decimal WasteBankTotalAmount { get; set; }
    public List<ExecutiveStudentCareRowViewModel> Rows { get; set; } = [];
    public List<ExecutiveStudentCareGroupSummaryViewModel> ClassroomSummaries { get; set; } = [];
    public List<ExecutiveStudentCareApprovalItemViewModel> PendingApprovals { get; set; } = [];
}
