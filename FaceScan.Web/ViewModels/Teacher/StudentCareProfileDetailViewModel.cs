using FaceScan.Web.Models.Enums;

namespace FaceScan.Web.ViewModels.Teacher;

public class StudentCareProfileDetailViewModel
{
    public int StudentId { get; set; }
    public string StudentCode { get; set; } = string.Empty;
    public string StudentName { get; set; } = string.Empty;
    public string GradeLevel { get; set; } = "-";
    public string Classroom { get; set; } = "-";
    public string? StudentNo { get; set; }
    public string? Address { get; set; }
    public string? GuardianName { get; set; }
    public string? GuardianNationalId { get; set; }
    public string? GuardianPhone { get; set; }
    public int BehaviorScore { get; set; }
    public int BehaviorTransactionCount { get; set; }
    public int GoodnessPoint { get; set; }
    public int GoodnessTransactionCount { get; set; }
    public decimal WasteBankWeightKg { get; set; }
    public decimal WasteBankAmount { get; set; }
    public int WasteBankTransactionCount { get; set; }
    public StudentCareRiskLevel RiskLevel { get; set; } = StudentCareRiskLevel.Normal;
    public string? LivesWith { get; set; }
    public string? GuardianRelationship { get; set; }
    public decimal? HouseholdIncome { get; set; }
    public string? RiskBehaviors { get; set; }
    public string? ProblemsFound { get; set; }
    public string? SupportRecommendation { get; set; }
    public DateTime? LastHomeVisitAt { get; set; }
    public decimal? HomeLatitude { get; set; }
    public decimal? HomeLongitude { get; set; }
    public DateTime? HomeLocationSharedAt { get; set; }
    public string? MapUrl { get; set; }
    public bool IsPrintMode { get; set; }
    public string BackArea { get; set; } = "Teacher";
    public string BackController { get; set; } = "StudentCare";
    public string BackAction { get; set; } = "Index";
    public List<StudentCareGuardianDetailViewModel> Guardians { get; set; } = [];
    public List<StudentCareBehaviorDetailViewModel> BehaviorTransactions { get; set; } = [];
    public List<StudentCareGoodnessDetailViewModel> GoodnessTransactions { get; set; } = [];
    public List<StudentCareHomeVisitDetailViewModel> HomeVisits { get; set; } = [];
    public List<StudentCareWasteBankDetailViewModel> WasteBankTransactions { get; set; } = [];
    public List<StudentCareFollowUpCaseDetailViewModel> FollowUpCases { get; set; } = [];
    public StudentCareFollowUpCaseInputViewModel FollowUpInput { get; set; } = new();
}

public class StudentCareBehaviorDetailViewModel
{
    public DateTime RecordedAt { get; set; }
    public string Category { get; set; } = string.Empty;
    public int ScoreChange { get; set; }
    public string Reason { get; set; } = string.Empty;
    public StudentCareRecordStatus Status { get; set; }
}

public class StudentCareGoodnessDetailViewModel
{
    public DateTime RecordedAt { get; set; }
    public string GoodnessType { get; set; } = string.Empty;
    public int Point { get; set; }
    public string? Description { get; set; }
    public StudentCareRecordStatus Status { get; set; }
}

public class StudentCareWasteBankDetailViewModel
{
    public DateTime RecordedAt { get; set; }
    public string WasteType { get; set; } = string.Empty;
    public decimal WeightKg { get; set; }
    public decimal PricePerKg { get; set; }
    public decimal Amount { get; set; }
    public string? Note { get; set; }
}
