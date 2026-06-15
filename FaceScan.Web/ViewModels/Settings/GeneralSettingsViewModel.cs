using System.ComponentModel.DataAnnotations;

namespace FaceScan.Web.ViewModels.Settings;

public class GeneralSettingsViewModel
{
    [Range(1, 30)]
    public int DuplicateWindowMinutes { get; set; }

    [Required]
    public string LateAfterTime { get; set; } = "08:00";

    [Required]
    public string CheckOutStartTime { get; set; } = "15:30";

    [Required]
    public string TeacherLateAfterTime { get; set; } = "08:00";

    [Required]
    public string TeacherCheckOutStartTime { get; set; } = "15:30";

    public bool SaveSnapshots { get; set; }

    [Range(0.1, 1.0)]
    public decimal FaceConfidenceThreshold { get; set; }

    public bool AllowManualOverride { get; set; }
    public bool EnableStudentCareModule { get; set; }
    public bool EnableBehaviorScoreModule { get; set; }
    public bool EnableGoodnessBankModule { get; set; }
    public bool EnableHomeVisitModule { get; set; }
    public bool EnableWasteBankModule { get; set; }

    [Range(1, 500)]
    public int StudentCareInitialBehaviorScore { get; set; } = 100;

    [Range(0, 500)]
    public int StudentCareLowBehaviorScoreThreshold { get; set; } = 60;

    public bool RequireStudentCareApproval { get; set; }
    public int? AcademicYearCurrentId { get; set; }
}
