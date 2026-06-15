using System.Security.Claims;
using FaceScan.Web.Data;
using FaceScan.Web.Helpers;
using FaceScan.Web.Models.Entities;
using FaceScan.Web.Services.Interfaces;
using FaceScan.Web.ViewModels;
using FaceScan.Web.ViewModels.Teacher;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FaceScan.Web.Areas.Teacher.Controllers;

[Area("Teacher")]
[Authorize(Roles = "SuperAdmin,Admin,WasteBankStaff")]
public class WasteBankController : Controller
{
    private readonly ApplicationDbContext _dbContext;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ISystemSettingService _systemSettingService;
    private readonly IAuditLogService _auditLogService;

    public WasteBankController(
        ApplicationDbContext dbContext,
        UserManager<ApplicationUser> userManager,
        ISystemSettingService systemSettingService,
        IAuditLogService auditLogService)
    {
        _dbContext = dbContext;
        _userManager = userManager;
        _systemSettingService = systemSettingService;
        _auditLogService = auditLogService;
    }

    [HttpGet]
    public async Task<IActionResult> Index([FromQuery] WasteBankFilterViewModel filter, CancellationToken cancellationToken)
    {
        var settings = await _systemSettingService.GetSettingsAsync(cancellationToken);
        if (!settings.EnableWasteBankModule)
        {
            return NotFound();
        }

        var students = await GetFilteredStudentsAsync(filter, cancellationToken);
        var studentIds = students.Select(x => x.Id).ToList();
        var totals = await _dbContext.WasteBankTransactions
            .AsNoTracking()
            .Where(x => studentIds.Contains(x.StudentId))
            .Where(x => !settings.AcademicYearCurrentId.HasValue || x.AcademicYearId == settings.AcademicYearCurrentId.Value)
            .GroupBy(x => x.StudentId)
            .Select(x => new
            {
                StudentId = x.Key,
                Weight = x.Sum(t => t.WeightKg),
                Amount = x.Sum(t => t.Amount),
                Count = x.Count()
            })
            .ToDictionaryAsync(x => x.StudentId, cancellationToken);

        var model = new WasteBankDashboardViewModel
        {
            Filter = filter,
            GradeLevelOptions = await BuildGradeLevelOptionsAsync(cancellationToken),
            ClassroomOptions = await BuildClassroomOptionsAsync(filter.GradeLevelId, cancellationToken),
            ActiveWasteRates = await BuildActiveWasteRatesAsync(cancellationToken),
            StudentOptions = students
                .Select(x => new SelectOptionViewModel { Value = x.Id, Text = $"{x.StudentCode} - {x.FullName}" })
                .ToList(),
            Students = students.Select(student =>
            {
                totals.TryGetValue(student.Id, out var total);
                return new StudentCareWasteBankReportRowViewModel
                {
                    StudentId = student.Id,
                    StudentCode = student.StudentCode,
                    FullName = student.FullName,
                    Classroom = student.Classroom?.Name ?? "-",
                    TotalWeightKg = total?.Weight ?? 0m,
                    TotalAmount = total?.Amount ?? 0m,
                    TransactionCount = total?.Count ?? 0
                };
            }).ToList()
        };
        model.TotalWeightKg = model.Students.Sum(x => x.TotalWeightKg);
        model.TotalAmount = model.Students.Sum(x => x.TotalAmount);

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveRate(WasteBankRateInputViewModel input, CancellationToken cancellationToken)
    {
        var settings = await _systemSettingService.GetSettingsAsync(cancellationToken);
        if (!settings.EnableWasteBankModule)
        {
            return NotFound();
        }

        if (!ModelState.IsValid || string.IsNullOrWhiteSpace(input.WasteType) || input.PricePerKg <= 0)
        {
            TempData["Error"] = "กรุณาระบุชนิดขยะและราคาต่อกิโลกรัมให้ถูกต้อง";
            return RedirectToAction(nameof(Index));
        }

        var wasteType = input.WasteType.Trim();
        var now = DateTime.UtcNow;
        var activeRates = await _dbContext.WasteBankRates
            .Where(x => x.IsActive && x.WasteType == wasteType)
            .ToListAsync(cancellationToken);

        foreach (var rate in activeRates)
        {
            rate.IsActive = false;
            rate.EffectiveTo = now;
        }

        var newRate = new WasteBankRate
        {
            WasteType = wasteType,
            PricePerKg = input.PricePerKg,
            EffectiveFrom = now,
            IsActive = true
        };

        _dbContext.WasteBankRates.Add(newRate);
        await _dbContext.SaveChangesAsync(cancellationToken);

        await _auditLogService.LogAsync(
            User.FindFirstValue(ClaimTypes.NameIdentifier),
            "UpdateWasteBankRate",
            "WasteBankRate",
            newRate.Id.ToString(),
            $"{newRate.WasteType}: {newRate.PricePerKg:N2} per kg",
            HttpContextHelper.GetIpAddress(HttpContext),
            cancellationToken);

        TempData["Success"] = "บันทึกราคารับซื้อขยะเรียบร้อย";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RecordTransaction(WasteBankTransactionInputViewModel input, CancellationToken cancellationToken)
    {
        var settings = await _systemSettingService.GetSettingsAsync(cancellationToken);
        if (!settings.EnableWasteBankModule)
        {
            return NotFound();
        }

        if (!ModelState.IsValid || input.WeightKg <= 0 || string.IsNullOrWhiteSpace(input.WasteType))
        {
            TempData["Error"] = "กรุณาระบุนักเรียน ชนิดขยะ และน้ำหนักให้ถูกต้อง";
            return RedirectToAction(nameof(Index));
        }

        var student = await _dbContext.Students
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == input.StudentId && x.IsActive, cancellationToken);
        if (student is null)
        {
            return Forbid();
        }

        var academicYearId = settings.AcademicYearCurrentId ?? student.AcademicYearId;
        var wasteType = input.WasteType.Trim();
        var rate = await _dbContext.WasteBankRates
            .AsNoTracking()
            .Where(x => x.IsActive && x.WasteType == wasteType)
            .OrderByDescending(x => x.EffectiveFrom)
            .FirstOrDefaultAsync(cancellationToken);
        if (rate is null)
        {
            TempData["Error"] = "ยังไม่ได้ตั้งราคารับซื้อขยะชนิดนี้";
            return RedirectToAction(nameof(Index));
        }

        var user = await _userManager.GetUserAsync(User);
        var amount = Math.Round(input.WeightKg * rate.PricePerKg, 2, MidpointRounding.AwayFromZero);
        var transaction = new WasteBankTransaction
        {
            StudentId = student.Id,
            AcademicYearId = academicYearId,
            WasteType = wasteType,
            WeightKg = input.WeightKg,
            PricePerKg = rate.PricePerKg,
            Amount = amount,
            RecordedByUserId = user?.Id,
            RecordedAt = DateTime.UtcNow,
            Note = input.Note?.Trim()
        };

        _dbContext.WasteBankTransactions.Add(transaction);
        await _dbContext.SaveChangesAsync(cancellationToken);

        await _auditLogService.LogAsync(
            User.FindFirstValue(ClaimTypes.NameIdentifier),
            "RecordWasteBank",
            "WasteBankTransaction",
            transaction.Id.ToString(),
            $"{student.StudentCode} {student.FullName}: {transaction.WasteType} {transaction.WeightKg:N3} kg = {transaction.Amount:N2}",
            HttpContextHelper.GetIpAddress(HttpContext),
            cancellationToken);

        TempData["Success"] = $"บันทึกฝากขยะเรียบร้อย ยอดเงิน {amount:N2} บาท";
        return RedirectToAction(nameof(Index));
    }

    private async Task<List<Student>> GetFilteredStudentsAsync(WasteBankFilterViewModel filter, CancellationToken cancellationToken)
    {
        var query = _dbContext.Students
            .AsNoTracking()
            .Include(x => x.Classroom)
            .Where(x => x.IsActive);

        if (filter.GradeLevelId.HasValue)
        {
            query = query.Where(x => x.GradeLevelId == filter.GradeLevelId.Value);
        }

        if (filter.ClassroomId.HasValue)
        {
            query = query.Where(x => x.ClassroomId == filter.ClassroomId.Value);
        }

        var keyword = filter.Keyword?.Trim();
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            query = query.Where(x =>
                x.StudentCode.Contains(keyword) ||
                x.FirstName.Contains(keyword) ||
                x.LastName.Contains(keyword) ||
                (x.FirstName + " " + x.LastName).Contains(keyword) ||
                (x.Prefix + x.FirstName + " " + x.LastName).Contains(keyword));
        }

        return await query
            .OrderBy(x => x.Classroom!.Name)
            .ThenBy(x => x.StudentNo)
            .ThenBy(x => x.StudentCode)
            .ToListAsync(cancellationToken);
    }

    private async Task<List<WasteBankRateViewModel>> BuildActiveWasteRatesAsync(CancellationToken cancellationToken)
    {
        return await _dbContext.WasteBankRates
            .AsNoTracking()
            .Where(x => x.IsActive)
            .OrderBy(x => x.WasteType)
            .Select(x => new WasteBankRateViewModel
            {
                WasteType = x.WasteType,
                PricePerKg = x.PricePerKg,
                EffectiveFrom = x.EffectiveFrom
            })
            .ToListAsync(cancellationToken);
    }

    private async Task<List<SelectOptionViewModel>> BuildGradeLevelOptionsAsync(CancellationToken cancellationToken)
    {
        return await _dbContext.GradeLevels
            .AsNoTracking()
            .Where(x => x.IsActive)
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.Name)
            .Select(x => new SelectOptionViewModel { Value = x.Id, Text = x.Name })
            .ToListAsync(cancellationToken);
    }

    private async Task<List<SelectOptionViewModel>> BuildClassroomOptionsAsync(int? gradeLevelId, CancellationToken cancellationToken)
    {
        var query = _dbContext.Classrooms
            .AsNoTracking()
            .Where(x => x.IsActive);

        if (gradeLevelId.HasValue)
        {
            query = query.Where(x => x.GradeLevelId == gradeLevelId.Value);
        }

        return await query
            .OrderBy(x => x.Name)
            .Select(x => new SelectOptionViewModel { Value = x.Id, Text = x.Name })
            .ToListAsync(cancellationToken);
    }
}
