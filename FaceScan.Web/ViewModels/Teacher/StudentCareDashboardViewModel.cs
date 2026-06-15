namespace FaceScan.Web.ViewModels.Teacher;

public class StudentCareDashboardViewModel
{
    public string TeacherName { get; set; } = string.Empty;
    public string AssignedClassroomName { get; set; } = "-";
    public StudentCareDashboardFilterViewModel Filter { get; set; } = new();
    public bool CanFilterAllStudents { get; set; }
    public bool IsStudentCareEnabled { get; set; }
    public bool IsBehaviorScoreEnabled { get; set; }
    public bool IsGoodnessBankEnabled { get; set; }
    public bool IsHomeVisitEnabled { get; set; }
    public bool IsWasteBankEnabled { get; set; }
    public int StudentCount { get; set; }
    public int ProfileCount { get; set; }
    public int HomeVisitedCount { get; set; }
    public int RiskStudentCount { get; set; }
    public int UrgentRiskStudentCount { get; set; }
    public int OpenFollowUpCaseCount { get; set; }
    public int OverdueFollowUpCaseCount { get; set; }
    public int DueTodayFollowUpCaseCount { get; set; }
    public decimal WasteBankTotalWeightKg { get; set; }
    public decimal WasteBankTotalAmount { get; set; }
    public StudentCareTransactionInputViewModel BehaviorInput { get; set; } = new();
    public StudentCareTransactionInputViewModel GoodnessInput { get; set; } = new();
    public WasteBankRateInputViewModel WasteRateInput { get; set; } = new();
    public WasteBankTransactionInputViewModel WasteTransactionInput { get; set; } = new();
    public List<SelectOptionViewModel> GradeLevelOptions { get; set; } = [];
    public List<StudentCareClassroomOptionViewModel> ClassroomOptions { get; set; } = [];
    public List<StudentCareStudentOptionViewModel> StudentOptions { get; set; } = [];
    public List<StudentCareCategoryOptionViewModel> BehaviorCategoryOptions { get; set; } = [];
    public List<StudentCareCategoryOptionViewModel> GoodnessCategoryOptions { get; set; } = [];
    public List<WasteBankRateViewModel> ActiveWasteRates { get; set; } = [];
    public List<StudentCareFollowUpReminderViewModel> FollowUpReminders { get; set; } = [];
    public List<StudentCareDashboardStudentViewModel> Students { get; set; } = [];
}
