using FaceScan.Web.Helpers;
using FaceScan.Web.Services.Interfaces;
using FaceScan.Web.ViewModels.Settings;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FaceScan.Web.Controllers;

[Authorize(Roles = "SuperAdmin,Admin")]
public class SettingsController : Controller
{
    private readonly ISystemSettingService _systemSettingService;
    private readonly IStudentService _studentService;

    public SettingsController(ISystemSettingService systemSettingService, IStudentService studentService)
    {
        _systemSettingService = systemSettingService;
        _studentService = studentService;
    }

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var model = await BuildPageModelAsync(cancellationToken);
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveBranding(BrandingSettingsViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View("Index", await BuildBrandingErrorModelAsync(model, cancellationToken));
        }

        var result = await _systemSettingService.UpdateBrandingAsync(
            model,
            User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value,
            HttpContextHelper.GetIpAddress(HttpContext),
            cancellationToken);

        TempData[result.Success ? "Success" : "Error"] = result.Message;
        if (!result.Success)
        {
            return View("Index", await BuildBrandingErrorModelAsync(model, cancellationToken));
        }

        return Redirect($"{Url.Action(nameof(Index))}#brandingSettings");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveGeneral(GeneralSettingsViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View("Index", await BuildGeneralErrorModelAsync(model, cancellationToken));
        }

        var result = await _systemSettingService.UpdateGeneralAsync(
            model,
            User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value,
            HttpContextHelper.GetIpAddress(HttpContext),
            cancellationToken);

        TempData[result.Success ? "Success" : "Error"] = result.Message;
        if (!result.Success)
        {
            return View("Index", await BuildGeneralErrorModelAsync(model, cancellationToken));
        }

        return Redirect($"{Url.Action(nameof(Index))}#generalSettings");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SavePeriods(PeriodSettingsViewModel model, CancellationToken cancellationToken)
    {
        var result = await _systemSettingService.UpdatePeriodsAsync(
            model,
            User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value,
            HttpContextHelper.GetIpAddress(HttpContext),
            cancellationToken);

        TempData[result.Success ? "Success" : "Error"] = result.Message;
        if (!result.Success)
        {
            return View("Index", await BuildPeriodErrorModelAsync(model, cancellationToken));
        }

        return Redirect($"{Url.Action(nameof(Index))}#periodSettings");
    }

    private async Task<SystemSettingsViewModel> BuildPageModelAsync(CancellationToken cancellationToken)
    {
        var model = await _systemSettingService.GetSettingsViewModelAsync(cancellationToken);
        await PopulateViewDataAsync(model, cancellationToken);
        return model;
    }

    private async Task<SystemSettingsViewModel> BuildBrandingErrorModelAsync(BrandingSettingsViewModel source, CancellationToken cancellationToken)
    {
        var model = await BuildPageModelAsync(cancellationToken);
        model.ApplicationDisplayName = source.ApplicationDisplayName;
        model.ApplicationTagline = source.ApplicationTagline;
        model.SchoolName = source.SchoolName;
        model.CurrentLogoPath = string.IsNullOrWhiteSpace(source.CurrentLogoPath) ? model.CurrentLogoPath : source.CurrentLogoPath;
        model.RemoveCurrentLogo = source.RemoveCurrentLogo;
        model.ThemePrimaryColor = source.ThemePrimaryColor;
        model.ThemePrimarySoftColor = source.ThemePrimarySoftColor;
        model.ThemeAccentColor = source.ThemeAccentColor;
        model.ThemeBackgroundColor = source.ThemeBackgroundColor;
        model.ThemeSurfaceColor = source.ThemeSurfaceColor;
        model.ThemeSidebarStartColor = source.ThemeSidebarStartColor;
        model.ThemeSidebarEndColor = source.ThemeSidebarEndColor;
        return model;
    }

    private async Task<SystemSettingsViewModel> BuildGeneralErrorModelAsync(GeneralSettingsViewModel source, CancellationToken cancellationToken)
    {
        var model = await BuildPageModelAsync(cancellationToken);
        model.DuplicateWindowMinutes = source.DuplicateWindowMinutes;
        model.LateAfterTime = source.LateAfterTime;
        model.CheckOutStartTime = source.CheckOutStartTime;
        model.TeacherLateAfterTime = source.TeacherLateAfterTime;
        model.TeacherCheckOutStartTime = source.TeacherCheckOutStartTime;
        model.SaveSnapshots = source.SaveSnapshots;
        model.FaceConfidenceThreshold = source.FaceConfidenceThreshold;
        model.AllowManualOverride = source.AllowManualOverride;
        model.EnableStudentCareModule = source.EnableStudentCareModule;
        model.EnableBehaviorScoreModule = source.EnableBehaviorScoreModule;
        model.EnableGoodnessBankModule = source.EnableGoodnessBankModule;
        model.EnableHomeVisitModule = source.EnableHomeVisitModule;
        model.EnableWasteBankModule = source.EnableWasteBankModule;
        model.StudentCareInitialBehaviorScore = source.StudentCareInitialBehaviorScore;
        model.StudentCareLowBehaviorScoreThreshold = source.StudentCareLowBehaviorScoreThreshold;
        model.RequireStudentCareApproval = source.RequireStudentCareApproval;
        model.AcademicYearCurrentId = source.AcademicYearCurrentId;
        return model;
    }

    private async Task<SystemSettingsViewModel> BuildPeriodErrorModelAsync(PeriodSettingsViewModel source, CancellationToken cancellationToken)
    {
        var model = await BuildPageModelAsync(cancellationToken);
        model.Periods = source.Periods ?? [];
        return model;
    }

    private async Task PopulateViewDataAsync(SystemSettingsViewModel model, CancellationToken cancellationToken)
    {
        ViewBag.AcademicYears = await _studentService.GetAcademicYearOptionsAsync(cancellationToken);

        if (!string.IsNullOrWhiteSpace(model.CurrentLogoPath))
        {
            return;
        }

        var current = await _systemSettingService.GetSettingsViewModelAsync(cancellationToken);
        model.CurrentLogoPath = current.CurrentLogoPath;
    }
}
