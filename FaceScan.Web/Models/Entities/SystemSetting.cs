namespace FaceScan.Web.Models.Entities;

public class SystemSetting : BaseEntity
{
    public int DuplicateWindowMinutes { get; set; } = 3;
    public TimeSpan LateAfterTime { get; set; } = new(8, 0, 0);
    public TimeSpan CheckOutStartTime { get; set; } = new(15, 30, 0);
    public TimeSpan TeacherLateAfterTime { get; set; } = new(8, 0, 0);
    public TimeSpan TeacherCheckOutStartTime { get; set; } = new(15, 30, 0);
    public bool SaveSnapshots { get; set; } = true;
    public decimal FaceConfidenceThreshold { get; set; } = 0.80m;
    public bool AllowManualOverride { get; set; }
    public string SchoolName { get; set; } = "FaceScan School";
    public string ApplicationDisplayName { get; set; } = "FaceScan";
    public string ApplicationTagline { get; set; } = "ระบบเช็กเวลาเข้า-ออกด้วยการสแกนใบหน้า";
    public string? BrandLogoPath { get; set; }
    public string ThemePrimaryColor { get; set; } = "#7C3AED";
    public string ThemePrimarySoftColor { get; set; } = "#A855F7";
    public string ThemeAccentColor { get; set; } = "#E9D5FF";
    public string ThemeBackgroundColor { get; set; } = "#F7F3FF";
    public string ThemeSurfaceColor { get; set; } = "#FFFFFF";
    public string ThemeSidebarStartColor { get; set; } = "#7C3AED";
    public string ThemeSidebarEndColor { get; set; } = "#4C1D95";
    public int? AcademicYearCurrentId { get; set; }

    public AcademicYear? AcademicYearCurrent { get; set; }
}
