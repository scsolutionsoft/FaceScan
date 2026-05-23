using Microsoft.AspNetCore.Http;
using System.ComponentModel.DataAnnotations;

namespace FaceScan.Web.ViewModels.Settings;

public class SystemSettingsViewModel
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

    [Required]
    [MaxLength(200)]
    public string SchoolName { get; set; } = string.Empty;

    [Required]
    [MaxLength(120)]
    public string ApplicationDisplayName { get; set; } = "FaceScan";

    [Required]
    [MaxLength(200)]
    public string ApplicationTagline { get; set; } = "ระบบเช็กเวลาเข้า-ออกด้วยการสแกนใบหน้า";

    public string? CurrentLogoPath { get; set; }
    public IFormFile? LogoFile { get; set; }
    public bool RemoveCurrentLogo { get; set; }

    [Required]
    [RegularExpression("^#[0-9A-Fa-f]{6}$", ErrorMessage = "กรุณาระบุสีในรูปแบบ #RRGGBB")]
    public string ThemePrimaryColor { get; set; } = "#7C3AED";

    [Required]
    [RegularExpression("^#[0-9A-Fa-f]{6}$", ErrorMessage = "กรุณาระบุสีในรูปแบบ #RRGGBB")]
    public string ThemePrimarySoftColor { get; set; } = "#A855F7";

    [Required]
    [RegularExpression("^#[0-9A-Fa-f]{6}$", ErrorMessage = "กรุณาระบุสีในรูปแบบ #RRGGBB")]
    public string ThemeAccentColor { get; set; } = "#E9D5FF";

    [Required]
    [RegularExpression("^#[0-9A-Fa-f]{6}$", ErrorMessage = "กรุณาระบุสีในรูปแบบ #RRGGBB")]
    public string ThemeBackgroundColor { get; set; } = "#F7F3FF";

    [Required]
    [RegularExpression("^#[0-9A-Fa-f]{6}$", ErrorMessage = "กรุณาระบุสีในรูปแบบ #RRGGBB")]
    public string ThemeSurfaceColor { get; set; } = "#FFFFFF";

    [Required]
    [RegularExpression("^#[0-9A-Fa-f]{6}$", ErrorMessage = "กรุณาระบุสีในรูปแบบ #RRGGBB")]
    public string ThemeSidebarStartColor { get; set; } = "#7C3AED";

    [Required]
    [RegularExpression("^#[0-9A-Fa-f]{6}$", ErrorMessage = "กรุณาระบุสีในรูปแบบ #RRGGBB")]
    public string ThemeSidebarEndColor { get; set; } = "#4C1D95";

    public int? AcademicYearCurrentId { get; set; }
    public List<PeriodVisibilityItemViewModel> Periods { get; set; } = [];
}
