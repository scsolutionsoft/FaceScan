using FaceScan.Web.Data;
using FaceScan.Web.Models.Entities;
using FaceScan.Web.Services.Interfaces;
using FaceScan.Web.ViewModels.Settings;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.EntityFrameworkCore;
using System.Text;

namespace FaceScan.Web.Services;

public class SystemSettingService : ISystemSettingService
{
    private const string SettingsCacheKey = "system-settings:current";
    private static readonly TimeSpan SettingsCacheDuration = TimeSpan.FromMinutes(5);

    private readonly ApplicationDbContext _dbContext;
    private readonly IAuditLogService _auditLogService;
    private readonly IFileStorageService _fileStorageService;
    private readonly IMemoryCache _memoryCache;

    public SystemSettingService(
        ApplicationDbContext dbContext,
        IAuditLogService auditLogService,
        IFileStorageService fileStorageService,
        IMemoryCache memoryCache)
    {
        _dbContext = dbContext;
        _auditLogService = auditLogService;
        _fileStorageService = fileStorageService;
        _memoryCache = memoryCache;
    }

    public async Task<SystemSetting> GetSettingsAsync(CancellationToken cancellationToken = default)
    {
        if (_memoryCache.TryGetValue<SystemSetting>(SettingsCacheKey, out var cachedSettings) &&
            cachedSettings is not null)
        {
            return CloneSettings(cachedSettings);
        }

        var settings = await EnsureSettingsRecordAsync(cancellationToken);
        var snapshot = CloneSettings(settings);
        _memoryCache.Set(
            SettingsCacheKey,
            snapshot,
            new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = SettingsCacheDuration
            });

        return CloneSettings(snapshot);
    }

    private async Task<SystemSetting> EnsureSettingsRecordAsync(CancellationToken cancellationToken = default)
    {
        var settings = await _dbContext.SystemSettings.FirstOrDefaultAsync(cancellationToken);
        if (settings is null)
        {
            settings = CreateDefaultSettings();
            _dbContext.SystemSettings.Add(settings);
            await _dbContext.SaveChangesAsync(cancellationToken);
            return settings;
        }

        var changed = false;
        changed |= EnsureString(settings, x => x.SchoolName, value => settings.SchoolName = value, "โรงเรียนตัวอย่าง FaceScan");
        changed |= EnsureString(settings, x => x.ApplicationDisplayName, value => settings.ApplicationDisplayName = value, "FaceScan");
        changed |= EnsureString(settings, x => x.ApplicationTagline, value => settings.ApplicationTagline = value, "ระบบเช็กเวลาเข้า-ออกด้วยการสแกนใบหน้า");
        changed |= EnsureColor(settings.ThemePrimaryColor, value => settings.ThemePrimaryColor = value, "#7C3AED");
        changed |= EnsureColor(settings.ThemePrimarySoftColor, value => settings.ThemePrimarySoftColor = value, "#A855F7");
        changed |= EnsureColor(settings.ThemeAccentColor, value => settings.ThemeAccentColor = value, "#E9D5FF");
        changed |= EnsureColor(settings.ThemeBackgroundColor, value => settings.ThemeBackgroundColor = value, "#F7F3FF");
        changed |= EnsureColor(settings.ThemeSurfaceColor, value => settings.ThemeSurfaceColor = value, "#FFFFFF");
        changed |= EnsureColor(settings.ThemeSidebarStartColor, value => settings.ThemeSidebarStartColor = value, "#7C3AED");
        changed |= EnsureColor(settings.ThemeSidebarEndColor, value => settings.ThemeSidebarEndColor = value, "#4C1D95");

        if (changed)
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        return settings;
    }

    private async Task<SystemSetting> GetTrackedSettingsEntityAsync(CancellationToken cancellationToken = default)
        => await EnsureSettingsRecordAsync(cancellationToken);

    public async Task<SystemSettingsViewModel> GetSettingsViewModelAsync(CancellationToken cancellationToken = default)
    {
        var settings = await GetTrackedSettingsEntityAsync(cancellationToken);
        return new SystemSettingsViewModel
        {
            DuplicateWindowMinutes = settings.DuplicateWindowMinutes,
            LateAfterTime = settings.LateAfterTime.ToString(@"hh\:mm"),
            CheckOutStartTime = settings.CheckOutStartTime.ToString(@"hh\:mm"),
            TeacherLateAfterTime = settings.TeacherLateAfterTime.ToString(@"hh\:mm"),
            TeacherCheckOutStartTime = settings.TeacherCheckOutStartTime.ToString(@"hh\:mm"),
            SaveSnapshots = settings.SaveSnapshots,
            FaceConfidenceThreshold = settings.FaceConfidenceThreshold,
            AllowManualOverride = settings.AllowManualOverride,
            EnableStudentCareModule = settings.EnableStudentCareModule,
            EnableBehaviorScoreModule = settings.EnableBehaviorScoreModule,
            EnableGoodnessBankModule = settings.EnableGoodnessBankModule,
            EnableHomeVisitModule = settings.EnableHomeVisitModule,
            EnableWasteBankModule = settings.EnableWasteBankModule,
            StudentCareInitialBehaviorScore = settings.StudentCareInitialBehaviorScore,
            StudentCareLowBehaviorScoreThreshold = settings.StudentCareLowBehaviorScoreThreshold,
            RequireStudentCareApproval = settings.RequireStudentCareApproval,
            SchoolName = settings.SchoolName,
            ApplicationDisplayName = settings.ApplicationDisplayName,
            ApplicationTagline = settings.ApplicationTagline,
            CurrentLogoPath = settings.BrandLogoPath,
            ThemePrimaryColor = settings.ThemePrimaryColor,
            ThemePrimarySoftColor = settings.ThemePrimarySoftColor,
            ThemeAccentColor = settings.ThemeAccentColor,
            ThemeBackgroundColor = settings.ThemeBackgroundColor,
            ThemeSurfaceColor = settings.ThemeSurfaceColor,
            ThemeSidebarStartColor = settings.ThemeSidebarStartColor,
            ThemeSidebarEndColor = settings.ThemeSidebarEndColor,
            AcademicYearCurrentId = settings.AcademicYearCurrentId,
            Periods = await _dbContext.ClassPeriods
                .AsNoTracking()
                .Where(x => x.IsActive)
                .OrderBy(x => x.SortOrder)
                .Select(x => new PeriodVisibilityItemViewModel
                {
                    Id = x.Id,
                    Name = x.Name,
                    SortOrder = x.SortOrder,
                    StartTime = x.StartTime.HasValue ? x.StartTime.Value.ToString(@"hh\:mm") : null,
                    EndTime = x.EndTime.HasValue ? x.EndTime.Value.ToString(@"hh\:mm") : null,
                    IsVisibleForCheck = x.IsVisibleForCheck
                })
                .ToListAsync(cancellationToken)
        };
    }

    public async Task<(bool Success, string Message)> UpdateBrandingAsync(
        BrandingSettingsViewModel model,
        string? userId,
        string? ipAddress,
        CancellationToken cancellationToken = default)
    {
        if (!AreBrandColorsValid(model))
        {
            return (false, "รูปแบบสีธีมไม่ถูกต้อง");
        }

        var settings = await GetSettingsAsync(cancellationToken);
        settings.ApplicationDisplayName = model.ApplicationDisplayName.Trim();
        settings.ApplicationTagline = model.ApplicationTagline.Trim();
        settings.SchoolName = model.SchoolName.Trim();
        settings.ThemePrimaryColor = model.ThemePrimaryColor.ToUpperInvariant();
        settings.ThemePrimarySoftColor = model.ThemePrimarySoftColor.ToUpperInvariant();
        settings.ThemeAccentColor = model.ThemeAccentColor.ToUpperInvariant();
        settings.ThemeBackgroundColor = model.ThemeBackgroundColor.ToUpperInvariant();
        settings.ThemeSurfaceColor = model.ThemeSurfaceColor.ToUpperInvariant();
        settings.ThemeSidebarStartColor = model.ThemeSidebarStartColor.ToUpperInvariant();
        settings.ThemeSidebarEndColor = model.ThemeSidebarEndColor.ToUpperInvariant();

        try
        {
            await UpdateBrandLogoAsync(settings, model.RemoveCurrentLogo, model.LogoFile, cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            return (false, ex.Message);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        InvalidateSettingsCache();
        await _auditLogService.LogAsync(
            userId,
            "UpdateBranding",
            "SystemSetting",
            settings.Id.ToString(),
            BuildBrandingAuditDetail(model),
            ipAddress,
            cancellationToken);

        return (true, "บันทึกแบรนด์และธีมเรียบร้อย");
    }

    public async Task<(bool Success, string Message)> UpdateGeneralAsync(
        GeneralSettingsViewModel model,
        string? userId,
        string? ipAddress,
        CancellationToken cancellationToken = default)
    {
        if (!TimeSpan.TryParse(model.LateAfterTime, out var lateAfterTime))
        {
            return (false, "รูปแบบเวลาเช็คมาสายไม่ถูกต้อง");
        }

        if (!TimeSpan.TryParse(model.CheckOutStartTime, out var checkOutTime))
        {
            return (false, "รูปแบบเวลาเริ่มเช็คกลับไม่ถูกต้อง");
        }

        if (!TimeSpan.TryParse(model.TeacherLateAfterTime, out var teacherLateAfterTime))
        {
            return (false, "รูปแบบเวลาเริ่มนับมาสายของครูไม่ถูกต้อง");
        }

        if (!TimeSpan.TryParse(model.TeacherCheckOutStartTime, out var teacherCheckOutTime))
        {
            return (false, "รูปแบบเวลาเริ่มเช็คกลับของครูไม่ถูกต้อง");
        }

        var settings = await GetTrackedSettingsEntityAsync(cancellationToken);
        settings.DuplicateWindowMinutes = model.DuplicateWindowMinutes;
        settings.LateAfterTime = lateAfterTime;
        settings.CheckOutStartTime = checkOutTime;
        settings.TeacherLateAfterTime = teacherLateAfterTime;
        settings.TeacherCheckOutStartTime = teacherCheckOutTime;
        settings.SaveSnapshots = model.SaveSnapshots;
        settings.FaceConfidenceThreshold = model.FaceConfidenceThreshold;
        settings.AllowManualOverride = model.AllowManualOverride;
        settings.EnableStudentCareModule = model.EnableStudentCareModule;
        settings.EnableBehaviorScoreModule = model.EnableStudentCareModule && model.EnableBehaviorScoreModule;
        settings.EnableGoodnessBankModule = model.EnableStudentCareModule && model.EnableGoodnessBankModule;
        settings.EnableHomeVisitModule = model.EnableStudentCareModule && model.EnableHomeVisitModule;
        settings.EnableWasteBankModule = model.EnableStudentCareModule && model.EnableWasteBankModule;
        settings.StudentCareInitialBehaviorScore = model.StudentCareInitialBehaviorScore;
        settings.StudentCareLowBehaviorScoreThreshold = model.StudentCareLowBehaviorScoreThreshold;
        settings.RequireStudentCareApproval = model.EnableStudentCareModule && model.RequireStudentCareApproval;
        settings.AcademicYearCurrentId = model.AcademicYearCurrentId;

        await _dbContext.SaveChangesAsync(cancellationToken);
        InvalidateSettingsCache();
        await _auditLogService.LogAsync(
            userId,
            "UpdateGeneralSettings",
            "SystemSetting",
            settings.Id.ToString(),
            BuildGeneralAuditDetail(model),
            ipAddress,
            cancellationToken);

        return (true, "บันทึกการตั้งค่าทั่วไปเรียบร้อย");
    }

    public async Task<(bool Success, string Message)> UpdatePeriodsAsync(
        PeriodSettingsViewModel model,
        string? userId,
        string? ipAddress,
        CancellationToken cancellationToken = default)
    {
        var validation = await ValidatePeriodsAsync(model.Periods ?? [], cancellationToken);
        if (!validation.Success)
        {
            return (false, validation.Message);
        }

        var settings = await GetTrackedSettingsEntityAsync(cancellationToken);
        var summary = await SyncPeriodsAsync(validation.Items, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
        InvalidateSettingsCache();

        await _auditLogService.LogAsync(
            userId,
            "UpdateClassPeriods",
            "SystemSetting",
            settings.Id.ToString(),
            BuildPeriodAuditDetail(summary),
            ipAddress,
            cancellationToken);

        return (true, "บันทึกคาบเรียนเรียบร้อย");
    }

    private async Task UpdateBrandLogoAsync(
        SystemSetting settings,
        bool removeCurrentLogo,
        IFormFile? logoFile,
        CancellationToken cancellationToken)
    {
        if (removeCurrentLogo && !string.IsNullOrWhiteSpace(settings.BrandLogoPath))
        {
            await _fileStorageService.DeleteFileIfExistsAsync(settings.BrandLogoPath);
            settings.BrandLogoPath = null;
        }

        if (logoFile is null || logoFile.Length == 0)
        {
            return;
        }

        var oldLogoPath = settings.BrandLogoPath;
        var newLogoPath = await _fileStorageService.SaveBrandLogoAsync(logoFile, cancellationToken);
        settings.BrandLogoPath = newLogoPath;

        if (!string.IsNullOrWhiteSpace(oldLogoPath) &&
            !string.Equals(oldLogoPath, newLogoPath, StringComparison.OrdinalIgnoreCase))
        {
            await _fileStorageService.DeleteFileIfExistsAsync(oldLogoPath);
        }
    }

    private async Task<(bool Success, string Message, List<NormalizedPeriodInput> Items)> ValidatePeriodsAsync(
        IReadOnlyList<PeriodVisibilityItemViewModel> periods,
        CancellationToken cancellationToken)
    {
        var normalized = new List<NormalizedPeriodInput>();
        foreach (var item in periods)
        {
            if (item.Id <= 0 &&
                string.IsNullOrWhiteSpace(item.Name) &&
                string.IsNullOrWhiteSpace(item.StartTime) &&
                string.IsNullOrWhiteSpace(item.EndTime) &&
                item.SortOrder <= 0)
            {
                continue;
            }

            var name = item.Name?.Trim() ?? string.Empty;
            if (!item.MarkedForDeletion && string.IsNullOrWhiteSpace(name))
            {
                return (false, "กรุณาระบุชื่อคาบเรียนให้ครบ", []);
            }

            if (!item.MarkedForDeletion && item.SortOrder <= 0)
            {
                return (false, $"คาบเรียน {name} ต้องมีลำดับมากกว่า 0", []);
            }

            if (!TryParseTime(item.StartTime, out var startTime))
            {
                return (false, $"เวลาเริ่มของคาบ {name} ไม่ถูกต้อง", []);
            }

            if (!TryParseTime(item.EndTime, out var endTime))
            {
                return (false, $"เวลาสิ้นสุดของคาบ {name} ไม่ถูกต้อง", []);
            }

            if (startTime.HasValue && endTime.HasValue && endTime <= startTime)
            {
                return (false, $"เวลาสิ้นสุดของคาบ {name} ต้องมากกว่าเวลาเริ่ม", []);
            }

            normalized.Add(new NormalizedPeriodInput
            {
                Id = item.Id,
                Name = name,
                SortOrder = item.SortOrder,
                StartTime = startTime,
                EndTime = endTime,
                IsVisibleForCheck = item.IsVisibleForCheck,
                MarkedForDeletion = item.MarkedForDeletion
            });
        }

        var retained = normalized.Where(x => !x.MarkedForDeletion).ToList();
        if (retained.Count == 0)
        {
            return (false, "ต้องมีคาบเรียนอย่างน้อย 1 คาบ", []);
        }

        var duplicateOrders = retained
            .GroupBy(x => x.SortOrder)
            .FirstOrDefault(x => x.Count() > 1);
        if (duplicateOrders is not null)
        {
            return (false, $"ลำดับคาบเรียนซ้ำกันที่ลำดับ {duplicateOrders.Key}", []);
        }

        var duplicateNames = retained
            .GroupBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(x => x.Count() > 1);
        if (duplicateNames is not null)
        {
            return (false, $"ชื่อคาบเรียนซ้ำกัน: {duplicateNames.Key}", []);
        }

        var deletedIds = normalized
            .Where(x => x.MarkedForDeletion && x.Id > 0)
            .Select(x => x.Id)
            .Distinct()
            .ToList();

        if (deletedIds.Count > 0)
        {
            var periodNames = await _dbContext.ClassPeriods
                .AsNoTracking()
                .Where(x => deletedIds.Contains(x.Id))
                .ToDictionaryAsync(x => x.Id, x => x.Name, cancellationToken);

            var usedPeriodIds = await _dbContext.PeriodAttendanceSessions
                .AsNoTracking()
                .Where(x => deletedIds.Contains(x.ClassPeriodId))
                .Select(x => x.ClassPeriodId)
                .Distinct()
                .ToListAsync(cancellationToken);

            if (usedPeriodIds.Count > 0)
            {
                var usedNames = usedPeriodIds
                    .Select(id => periodNames.TryGetValue(id, out var name) ? name : $"คาบ #{id}")
                    .ToList();
                return (false, $"ไม่สามารถลบคาบที่เคยมีข้อมูลเช็คแล้วได้: {string.Join(", ", usedNames)}", []);
            }
        }

        return (true, string.Empty, normalized);
    }

    private async Task<PeriodSyncSummary> SyncPeriodsAsync(
        IReadOnlyList<NormalizedPeriodInput> periodInputs,
        CancellationToken cancellationToken)
    {
        var summary = new PeriodSyncSummary();
        var trackedPeriods = await _dbContext.ClassPeriods
            .OrderBy(x => x.SortOrder)
            .ToListAsync(cancellationToken);

        var deleteIds = periodInputs
            .Where(x => x.MarkedForDeletion && x.Id > 0)
            .Select(x => x.Id)
            .Distinct()
            .ToHashSet();

        var periodsToDelete = trackedPeriods
            .Where(x => deleteIds.Contains(x.Id))
            .ToList();

        if (periodsToDelete.Count > 0)
        {
            summary.Deleted = periodsToDelete.Count;
            _dbContext.ClassPeriods.RemoveRange(periodsToDelete);
            trackedPeriods = trackedPeriods.Where(x => !deleteIds.Contains(x.Id)).ToList();
        }

        if (trackedPeriods.Count > 0)
        {
            var tempOrder = trackedPeriods.Max(x => x.SortOrder) + trackedPeriods.Count + 10;
            foreach (var tracked in trackedPeriods.OrderBy(x => x.SortOrder))
            {
                tracked.SortOrder = tempOrder++;
            }

            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        var retainedItems = periodInputs
            .Where(x => !x.MarkedForDeletion)
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var trackedById = trackedPeriods.ToDictionary(x => x.Id);
        foreach (var item in retainedItems)
        {
            if (item.Id > 0 && trackedById.TryGetValue(item.Id, out var existing))
            {
                existing.Name = item.Name;
                existing.SortOrder = item.SortOrder;
                existing.StartTime = item.StartTime;
                existing.EndTime = item.EndTime;
                existing.IsVisibleForCheck = item.IsVisibleForCheck;
                existing.IsActive = true;
                summary.Updated++;
                continue;
            }

            _dbContext.ClassPeriods.Add(new ClassPeriod
            {
                Name = item.Name,
                SortOrder = item.SortOrder,
                StartTime = item.StartTime,
                EndTime = item.EndTime,
                IsVisibleForCheck = item.IsVisibleForCheck,
                IsActive = true
            });
            summary.Created++;
        }

        return summary;
    }

    private static bool TryParseTime(string? value, out TimeSpan? time)
    {
        time = null;
        if (string.IsNullOrWhiteSpace(value))
        {
            return true;
        }

        if (!TimeSpan.TryParse(value, out var parsed))
        {
            return false;
        }

        time = parsed;
        return true;
    }

    private static bool EnsureString(SystemSetting _, Func<SystemSetting, string> selector, Action<string> apply, string fallback)
    {
        var current = selector(_);
        if (!string.IsNullOrWhiteSpace(current))
        {
            return false;
        }

        apply(fallback);
        return true;
    }

    private static bool EnsureColor(string? current, Action<string> apply, string fallback)
    {
        if (IsValidHexColor(current))
        {
            return false;
        }

        apply(fallback);
        return true;
    }

    private static bool AreBrandColorsValid(BrandingSettingsViewModel model)
        => IsValidHexColor(model.ThemePrimaryColor) &&
           IsValidHexColor(model.ThemePrimarySoftColor) &&
           IsValidHexColor(model.ThemeAccentColor) &&
           IsValidHexColor(model.ThemeBackgroundColor) &&
           IsValidHexColor(model.ThemeSurfaceColor) &&
           IsValidHexColor(model.ThemeSidebarStartColor) &&
           IsValidHexColor(model.ThemeSidebarEndColor);

    private static bool IsValidHexColor(string? value)
        => !string.IsNullOrWhiteSpace(value) &&
           value.Length == 7 &&
           value[0] == '#' &&
           value.Skip(1).All(Uri.IsHexDigit);

    private static string BuildBrandingAuditDetail(BrandingSettingsViewModel model)
    {
        var builder = new StringBuilder();
        builder.AppendLine("อัปเดตแบรนด์และธีม");
        builder.AppendLine($"ชื่อโปรแกรม: {model.ApplicationDisplayName}");
        builder.AppendLine($"ชื่อโรงเรียน: {model.SchoolName}");
        builder.AppendLine($"คำอธิบาย: {model.ApplicationTagline}");
        builder.AppendLine($"โลโก้: {(model.RemoveCurrentLogo ? "ลบโลโก้เดิม" : model.LogoFile is null ? "ไม่เปลี่ยนแปลง" : "อัปโหลดโลโก้ใหม่")}");
        builder.AppendLine($"ธีม: {model.ThemePrimaryColor}, {model.ThemePrimarySoftColor}, {model.ThemeAccentColor}, {model.ThemeBackgroundColor}");
        return builder.ToString().Trim();
    }

    private static string BuildGeneralAuditDetail(GeneralSettingsViewModel model)
    {
        var builder = new StringBuilder();
        builder.AppendLine("อัปเดตการตั้งค่าทั่วไป");
        builder.AppendLine($"ป้องกันสแกนซ้ำ: {model.DuplicateWindowMinutes} นาที");
        builder.AppendLine($"นักเรียนมาสาย: {model.LateAfterTime}");
        builder.AppendLine($"นักเรียนเช็คกลับ: {model.CheckOutStartTime}");
        builder.AppendLine($"ครูมาสาย: {model.TeacherLateAfterTime}");
        builder.AppendLine($"ครูเช็คกลับ: {model.TeacherCheckOutStartTime}");
        builder.AppendLine($"เกณฑ์ความมั่นใจใบหน้า: {model.FaceConfidenceThreshold}");
        builder.AppendLine($"บันทึกภาพสแนปช็อต: {(model.SaveSnapshots ? "เปิด" : "ปิด")}");
        builder.AppendLine($"อนุญาตแก้ไขด้วยมือ: {(model.AllowManualOverride ? "เปิด" : "ปิด")}");
        builder.AppendLine($"Student Care: {(model.EnableStudentCareModule ? "Enabled" : "Disabled")}");
        builder.AppendLine($"Behavior Score: {(model.EnableBehaviorScoreModule ? "Enabled" : "Disabled")}");
        builder.AppendLine($"Goodness Bank: {(model.EnableGoodnessBankModule ? "Enabled" : "Disabled")}");
        builder.AppendLine($"Home Visit: {(model.EnableHomeVisitModule ? "Enabled" : "Disabled")}");
        builder.AppendLine($"Waste Bank: {(model.EnableWasteBankModule ? "Enabled" : "Disabled")}");
        builder.AppendLine($"Student Care Initial Behavior Score: {model.StudentCareInitialBehaviorScore}");
        builder.AppendLine($"Student Care Low Threshold: {model.StudentCareLowBehaviorScoreThreshold}");
        builder.AppendLine($"Student Care Approval: {(model.RequireStudentCareApproval ? "Required" : "Not required")}");
        return builder.ToString().Trim();
    }

    private static string BuildPeriodAuditDetail(PeriodSyncSummary summary)
        => $"อัปเดตคาบเรียน: เพิ่ม {summary.Created}, แก้ไข {summary.Updated}, ลบ {summary.Deleted}";

    private static SystemSetting CreateDefaultSettings()
        => new()
        {
            DuplicateWindowMinutes = 3,
            LateAfterTime = new TimeSpan(8, 0, 0),
            CheckOutStartTime = new TimeSpan(15, 30, 0),
            TeacherLateAfterTime = new TimeSpan(8, 0, 0),
            TeacherCheckOutStartTime = new TimeSpan(15, 30, 0),
            SaveSnapshots = true,
            FaceConfidenceThreshold = 0.8m,
            AllowManualOverride = false,
            EnableStudentCareModule = false,
            EnableBehaviorScoreModule = false,
            EnableGoodnessBankModule = false,
            EnableHomeVisitModule = false,
            EnableWasteBankModule = false,
            StudentCareInitialBehaviorScore = 100,
            StudentCareLowBehaviorScoreThreshold = 60,
            RequireStudentCareApproval = false,
            SchoolName = "โรงเรียนตัวอย่าง FaceScan",
            ApplicationDisplayName = "FaceScan",
            ApplicationTagline = "ระบบเช็กเวลาเข้า-ออกด้วยการสแกนใบหน้า",
            ThemePrimaryColor = "#7C3AED",
            ThemePrimarySoftColor = "#A855F7",
            ThemeAccentColor = "#E9D5FF",
            ThemeBackgroundColor = "#F7F3FF",
            ThemeSurfaceColor = "#FFFFFF",
            ThemeSidebarStartColor = "#7C3AED",
            ThemeSidebarEndColor = "#4C1D95"
        };

    private void InvalidateSettingsCache()
    {
        _memoryCache.Remove(SettingsCacheKey);
    }

    private static SystemSetting CloneSettings(SystemSetting source)
        => new()
        {
            Id = source.Id,
            DuplicateWindowMinutes = source.DuplicateWindowMinutes,
            LateAfterTime = source.LateAfterTime,
            CheckOutStartTime = source.CheckOutStartTime,
            TeacherLateAfterTime = source.TeacherLateAfterTime,
            TeacherCheckOutStartTime = source.TeacherCheckOutStartTime,
            SaveSnapshots = source.SaveSnapshots,
            FaceConfidenceThreshold = source.FaceConfidenceThreshold,
            AllowManualOverride = source.AllowManualOverride,
            EnableStudentCareModule = source.EnableStudentCareModule,
            EnableBehaviorScoreModule = source.EnableBehaviorScoreModule,
            EnableGoodnessBankModule = source.EnableGoodnessBankModule,
            EnableHomeVisitModule = source.EnableHomeVisitModule,
            EnableWasteBankModule = source.EnableWasteBankModule,
            StudentCareInitialBehaviorScore = source.StudentCareInitialBehaviorScore,
            StudentCareLowBehaviorScoreThreshold = source.StudentCareLowBehaviorScoreThreshold,
            RequireStudentCareApproval = source.RequireStudentCareApproval,
            SchoolName = source.SchoolName,
            ApplicationDisplayName = source.ApplicationDisplayName,
            ApplicationTagline = source.ApplicationTagline,
            BrandLogoPath = source.BrandLogoPath,
            ThemePrimaryColor = source.ThemePrimaryColor,
            ThemePrimarySoftColor = source.ThemePrimarySoftColor,
            ThemeAccentColor = source.ThemeAccentColor,
            ThemeBackgroundColor = source.ThemeBackgroundColor,
            ThemeSurfaceColor = source.ThemeSurfaceColor,
            ThemeSidebarStartColor = source.ThemeSidebarStartColor,
            ThemeSidebarEndColor = source.ThemeSidebarEndColor,
            AcademicYearCurrentId = source.AcademicYearCurrentId,
            CreatedAt = source.CreatedAt,
            UpdatedAt = source.UpdatedAt
        };

    private sealed class NormalizedPeriodInput
    {
        public int Id { get; init; }
        public string Name { get; init; } = string.Empty;
        public int SortOrder { get; init; }
        public TimeSpan? StartTime { get; init; }
        public TimeSpan? EndTime { get; init; }
        public bool IsVisibleForCheck { get; init; }
        public bool MarkedForDeletion { get; init; }
    }

    private sealed class PeriodSyncSummary
    {
        public int Created { get; set; }
        public int Updated { get; set; }
        public int Deleted { get; set; }
    }
}
