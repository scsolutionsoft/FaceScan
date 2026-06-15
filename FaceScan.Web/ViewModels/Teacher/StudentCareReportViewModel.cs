namespace FaceScan.Web.ViewModels.Teacher;

public class StudentCareReportViewModel
{
    public string AssignedClassroomName { get; set; } = "-";
    public int BehaviorLowThreshold { get; set; } = 60;
    public List<StudentCareBehaviorReportRowViewModel> LowBehaviorScoreStudents { get; set; } = [];
    public List<StudentCareScoreTransactionReportRowViewModel> BehaviorDeductionTransactions { get; set; } = [];
    public List<StudentCareGoodnessReportRowViewModel> TopGoodnessStudents { get; set; } = [];
    public List<StudentCareScoreTransactionReportRowViewModel> RecentGoodnessTransactions { get; set; } = [];
    public List<StudentCareHomeVisitReportRowViewModel> NotVisitedStudents { get; set; } = [];
    public List<StudentCareHomeVisitReportRowViewModel> RiskStudents { get; set; } = [];
    public List<StudentCareWasteBankReportRowViewModel> WasteBankTopStudents { get; set; } = [];
    public List<StudentCareFollowUpReportRowViewModel> OpenFollowUpCases { get; set; } = [];
    public decimal WasteBankTotalWeightKg { get; set; }
    public decimal WasteBankTotalAmount { get; set; }
}
