using FaceScan.Web.Models.Enums;

namespace FaceScan.Web.ViewModels.StudentPortal;

public class StudentPortalIndexViewModel
{
    public string StudentCode { get; set; } = string.Empty;
    public string StudentName { get; set; } = string.Empty;
    public string GradeLevel { get; set; } = string.Empty;
    public string Classroom { get; set; } = string.Empty;
    public string? ProfilePhotoPath { get; set; }
    public EnrollmentStatus EnrollmentStatus { get; set; }
    public int PhotoCount { get; set; }
    public List<StudentPortalProfilePhotoViewModel> ProfilePhotoOptions { get; set; } = [];
    public DateTime? TodayCheckIn { get; set; }
    public DateTime? TodayCheckOut { get; set; }
    public bool IsLateToday { get; set; }
    public TimeSpan LateAfterTime { get; set; }
    public bool IsStudentCareEnabled { get; set; }
    public bool IsBehaviorScoreEnabled { get; set; }
    public bool IsGoodnessBankEnabled { get; set; }
    public bool IsHomeVisitEnabled { get; set; }
    public bool IsWasteBankEnabled { get; set; }
    public bool HasHomeLocation { get; set; }
    public decimal? HomeLatitude { get; set; }
    public decimal? HomeLongitude { get; set; }
    public DateTime? HomeLocationSharedAt { get; set; }
    public int BehaviorScore { get; set; } = 100;
    public int GoodnessPoint { get; set; }
    public decimal WasteBankTotalWeightKg { get; set; }
    public decimal WasteBankTotalAmount { get; set; }
    public List<StudentPortalCareTransactionViewModel> RecentBehaviorTransactions { get; set; } = [];
    public List<StudentPortalCareTransactionViewModel> RecentGoodnessTransactions { get; set; } = [];
    public List<StudentPortalWasteBankTransactionViewModel> RecentWasteBankTransactions { get; set; } = [];
}

public class StudentPortalProfilePhotoViewModel
{
    public int PhotoId { get; set; }
    public string FilePath { get; set; } = string.Empty;
    public bool IsPrimary { get; set; }
    public DateTime CapturedAt { get; set; }
}
