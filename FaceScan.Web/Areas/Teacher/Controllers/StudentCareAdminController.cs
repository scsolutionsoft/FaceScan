using System.Security.Claims;
using FaceScan.Web.Data;
using FaceScan.Web.Helpers;
using FaceScan.Web.Models.Entities;
using FaceScan.Web.Services.Interfaces;
using FaceScan.Web.ViewModels.Teacher;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FaceScan.Web.Areas.Teacher.Controllers;

[Area("Teacher")]
[Authorize(Roles = "SuperAdmin,Admin,StudentCareAdmin")]
public class StudentCareAdminController : Controller
{
    private const string HomeroomRoleName = "HomeroomHead";

    private readonly ApplicationDbContext _dbContext;
    private readonly IAuditLogService _auditLogService;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<IdentityRole> _roleManager;

    public StudentCareAdminController(
        ApplicationDbContext dbContext,
        IAuditLogService auditLogService,
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole> roleManager)
    {
        _dbContext = dbContext;
        _auditLogService = auditLogService;
        _userManager = userManager;
        _roleManager = roleManager;
    }

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        return View(await BuildIndexViewModelAsync(cancellationToken));
    }

    [HttpGet]
    public async Task<IActionResult> HomeroomTeachers(CancellationToken cancellationToken)
    {
        return View(await BuildHomeroomTeachersViewModelAsync(cancellationToken));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveHomeroomTeacher(StudentCareHomeroomTeacherInputViewModel input, CancellationToken cancellationToken)
    {
        var isCreate = string.IsNullOrWhiteSpace(input.UserId);
        if (isCreate && string.IsNullOrWhiteSpace(input.Password))
        {
            ModelState.AddModelError(nameof(input.Password), "กรุณาระบุรหัสผ่านสำหรับบัญชีใหม่");
        }

        if (!input.AssignedClassroomId.HasValue || !await _dbContext.Classrooms.AnyAsync(x => x.Id == input.AssignedClassroomId.Value && x.IsActive, cancellationToken))
        {
            ModelState.AddModelError(nameof(input.AssignedClassroomId), "กรุณาเลือกห้องประจำชั้นที่ใช้งานอยู่");
        }

        if (!ModelState.IsValid)
        {
            TempData["Error"] = string.Join("; ", ModelState.Values.SelectMany(x => x.Errors).Select(x => x.ErrorMessage));
            return RedirectToAction(nameof(HomeroomTeachers));
        }

        if (!await _roleManager.RoleExistsAsync(HomeroomRoleName))
        {
            await _roleManager.CreateAsync(new IdentityRole(HomeroomRoleName));
        }

        var username = input.Username.Trim();
        var email = input.Email.Trim();
        var fullName = input.FullName.Trim();

        if (isCreate)
        {
            var duplicatedUser = await _userManager.FindByNameAsync(username);
            if (duplicatedUser is not null)
            {
                TempData["Error"] = "ชื่อผู้ใช้นี้ถูกใช้งานแล้ว";
                return RedirectToAction(nameof(HomeroomTeachers));
            }

            var user = new ApplicationUser
            {
                UserName = username,
                Email = email,
                FullName = fullName,
                EmailConfirmed = true,
                IsActive = input.IsActive,
                AssignedClassroomId = input.AssignedClassroomId
            };

            var createResult = await _userManager.CreateAsync(user, input.Password!.Trim());
            if (!createResult.Succeeded)
            {
                TempData["Error"] = string.Join("; ", createResult.Errors.Select(x => x.Description));
                return RedirectToAction(nameof(HomeroomTeachers));
            }

            await _userManager.AddToRoleAsync(user, HomeroomRoleName);
            await LogUserAsync("CreateHomeroomTeacher", user.Id, username, cancellationToken);
            TempData["Success"] = "เพิ่มครูโฮมรูมเรียบร้อย";
            return RedirectToAction(nameof(HomeroomTeachers));
        }

        var existingUser = await _userManager.FindByIdAsync(input.UserId!);
        if (existingUser is null)
        {
            TempData["Error"] = "ไม่พบบัญชีครูโฮมรูมที่ต้องการแก้ไข";
            return RedirectToAction(nameof(HomeroomTeachers));
        }

        var duplicate = await _userManager.FindByNameAsync(username);
        if (duplicate is not null && !string.Equals(duplicate.Id, existingUser.Id, StringComparison.Ordinal))
        {
            TempData["Error"] = "ชื่อผู้ใช้นี้ถูกใช้งานแล้ว";
            return RedirectToAction(nameof(HomeroomTeachers));
        }

        if (!string.Equals(existingUser.UserName, username, StringComparison.OrdinalIgnoreCase))
        {
            var setUsernameResult = await _userManager.SetUserNameAsync(existingUser, username);
            if (!setUsernameResult.Succeeded)
            {
                TempData["Error"] = string.Join("; ", setUsernameResult.Errors.Select(x => x.Description));
                return RedirectToAction(nameof(HomeroomTeachers));
            }
        }

        var setEmailResult = await _userManager.SetEmailAsync(existingUser, email);
        if (!setEmailResult.Succeeded)
        {
            TempData["Error"] = string.Join("; ", setEmailResult.Errors.Select(x => x.Description));
            return RedirectToAction(nameof(HomeroomTeachers));
        }

        existingUser.FullName = fullName;
        existingUser.IsActive = input.IsActive;
        existingUser.AssignedClassroomId = input.AssignedClassroomId;

        var updateResult = await _userManager.UpdateAsync(existingUser);
        if (!updateResult.Succeeded)
        {
            TempData["Error"] = string.Join("; ", updateResult.Errors.Select(x => x.Description));
            return RedirectToAction(nameof(HomeroomTeachers));
        }

        if (!await _userManager.IsInRoleAsync(existingUser, HomeroomRoleName))
        {
            await _userManager.AddToRoleAsync(existingUser, HomeroomRoleName);
        }

        if (!string.IsNullOrWhiteSpace(input.Password))
        {
            var token = await _userManager.GeneratePasswordResetTokenAsync(existingUser);
            var resetResult = await _userManager.ResetPasswordAsync(existingUser, token, input.Password.Trim());
            if (!resetResult.Succeeded)
            {
                TempData["Error"] = string.Join("; ", resetResult.Errors.Select(x => x.Description));
                return RedirectToAction(nameof(HomeroomTeachers));
            }
        }

        await LogUserAsync("UpdateHomeroomTeacher", existingUser.Id, username, cancellationToken);
        TempData["Success"] = "บันทึกข้อมูลครูโฮมรูมเรียบร้อย";
        return RedirectToAction(nameof(HomeroomTeachers));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleHomeroomTeacher(string id, CancellationToken cancellationToken)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user is null)
        {
            TempData["Error"] = "ไม่พบบัญชีครูโฮมรูมที่ต้องการเปลี่ยนสถานะ";
            return RedirectToAction(nameof(HomeroomTeachers));
        }

        if (!await _userManager.IsInRoleAsync(user, HomeroomRoleName))
        {
            TempData["Error"] = "บัญชีนี้ไม่ใช่ครูโฮมรูม";
            return RedirectToAction(nameof(HomeroomTeachers));
        }

        user.IsActive = !user.IsActive;
        await _userManager.UpdateAsync(user);
        await LogUserAsync(user.IsActive ? "ActivateHomeroomTeacher" : "DeactivateHomeroomTeacher", user.Id, user.UserName ?? id, cancellationToken);
        TempData["Success"] = user.IsActive ? "เปิดใช้งานครูโฮมรูมเรียบร้อย" : "ปิดใช้งานครูโฮมรูมเรียบร้อย";
        return RedirectToAction(nameof(HomeroomTeachers));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveBehaviorCategory(BehaviorScoreCategoryInputViewModel input, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid || input.ScoreChange == 0)
        {
            TempData["Error"] = "กรุณาระบุประเภทและคะแนนพฤติกรรมให้ถูกต้อง";
            return RedirectToAction(nameof(Index));
        }

        var name = input.Name.Trim();
        var duplicated = await _dbContext.BehaviorScoreCategories
            .AnyAsync(x => x.Name == name && (!input.Id.HasValue || x.Id != input.Id.Value), cancellationToken);
        if (duplicated)
        {
            TempData["Error"] = "มีประเภทคะแนนพฤติกรรมนี้แล้ว";
            return RedirectToAction(nameof(Index));
        }

        var isCreate = !input.Id.HasValue;
        var category = input.Id.HasValue
            ? await _dbContext.BehaviorScoreCategories.FirstOrDefaultAsync(x => x.Id == input.Id.Value, cancellationToken)
            : null;

        if (category is null)
        {
            category = new BehaviorScoreCategory();
            _dbContext.BehaviorScoreCategories.Add(category);
        }

        category.Name = name;
        category.ScoreChange = input.ScoreChange;
        category.Description = input.Description?.Trim();
        category.SortOrder = isCreate || category.SortOrder <= 0
            ? await GetNextBehaviorSortOrderAsync(cancellationToken)
            : category.SortOrder;
        category.IsActive = input.IsActive;

        await _dbContext.SaveChangesAsync(cancellationToken);
        await LogAsync(input.Id.HasValue ? "UpdateBehaviorScoreCategory" : "CreateBehaviorScoreCategory", category.Id, category.Name, cancellationToken);
        TempData["Success"] = "บันทึกประเภทคะแนนพฤติกรรมเรียบร้อย";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteBehaviorCategory(int id, CancellationToken cancellationToken)
    {
        var category = await _dbContext.BehaviorScoreCategories.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (category is null)
        {
            return NotFound();
        }

        _dbContext.BehaviorScoreCategories.Remove(category);
        await _dbContext.SaveChangesAsync(cancellationToken);
        await LogAsync("DeleteBehaviorScoreCategory", id, category.Name, cancellationToken);
        TempData["Success"] = "ลบประเภทคะแนนพฤติกรรมเรียบร้อย";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveGoodnessCategory(GoodnessBankCategoryInputViewModel input, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid || input.Point <= 0)
        {
            TempData["Error"] = "กรุณาระบุประเภทความดีและคะแนนให้ถูกต้อง";
            return RedirectToAction(nameof(Index));
        }

        var name = input.Name.Trim();
        var duplicated = await _dbContext.GoodnessBankCategories
            .AnyAsync(x => x.Name == name && (!input.Id.HasValue || x.Id != input.Id.Value), cancellationToken);
        if (duplicated)
        {
            TempData["Error"] = "มีประเภทความดีนี้แล้ว";
            return RedirectToAction(nameof(Index));
        }

        var isCreate = !input.Id.HasValue;
        var category = input.Id.HasValue
            ? await _dbContext.GoodnessBankCategories.FirstOrDefaultAsync(x => x.Id == input.Id.Value, cancellationToken)
            : null;

        if (category is null)
        {
            category = new GoodnessBankCategory();
            _dbContext.GoodnessBankCategories.Add(category);
        }

        category.Name = name;
        category.Point = input.Point;
        category.Description = input.Description?.Trim();
        category.SortOrder = isCreate || category.SortOrder <= 0
            ? await GetNextGoodnessSortOrderAsync(cancellationToken)
            : category.SortOrder;
        category.IsActive = input.IsActive;

        await _dbContext.SaveChangesAsync(cancellationToken);
        await LogAsync(input.Id.HasValue ? "UpdateGoodnessBankCategory" : "CreateGoodnessBankCategory", category.Id, category.Name, cancellationToken);
        TempData["Success"] = "บันทึกประเภทธนาคารความดีเรียบร้อย";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteGoodnessCategory(int id, CancellationToken cancellationToken)
    {
        var category = await _dbContext.GoodnessBankCategories.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (category is null)
        {
            return NotFound();
        }

        _dbContext.GoodnessBankCategories.Remove(category);
        await _dbContext.SaveChangesAsync(cancellationToken);
        await LogAsync("DeleteGoodnessBankCategory", id, category.Name, cancellationToken);
        TempData["Success"] = "ลบประเภทธนาคารความดีเรียบร้อย";
        return RedirectToAction(nameof(Index));
    }

    private async Task<StudentCareAdminIndexViewModel> BuildIndexViewModelAsync(CancellationToken cancellationToken)
    {
        await NormalizeCategorySortOrdersAsync(cancellationToken);

        return new StudentCareAdminIndexViewModel
        {
            BehaviorCategories = await _dbContext.BehaviorScoreCategories
                .AsNoTracking()
                .OrderBy(x => x.SortOrder)
                .ThenBy(x => x.Id)
                .Select(x => new BehaviorScoreCategoryViewModel
                {
                    Id = x.Id,
                    Name = x.Name,
                    ScoreChange = x.ScoreChange,
                    Description = x.Description,
                    SortOrder = x.SortOrder,
                    IsActive = x.IsActive
                })
                .ToListAsync(cancellationToken),
            GoodnessCategories = await _dbContext.GoodnessBankCategories
                .AsNoTracking()
                .OrderBy(x => x.SortOrder)
                .ThenBy(x => x.Id)
                .Select(x => new GoodnessBankCategoryViewModel
                {
                    Id = x.Id,
                    Name = x.Name,
                    Point = x.Point,
                    Description = x.Description,
                    SortOrder = x.SortOrder,
                    IsActive = x.IsActive
                })
                .ToListAsync(cancellationToken)
        };
    }

    private async Task<int> GetNextBehaviorSortOrderAsync(CancellationToken cancellationToken)
    {
        var maxSortOrder = await _dbContext.BehaviorScoreCategories
            .Select(x => (int?)x.SortOrder)
            .MaxAsync(cancellationToken);

        return (maxSortOrder ?? 0) + 1;
    }

    private async Task<int> GetNextGoodnessSortOrderAsync(CancellationToken cancellationToken)
    {
        var maxSortOrder = await _dbContext.GoodnessBankCategories
            .Select(x => (int?)x.SortOrder)
            .MaxAsync(cancellationToken);

        return (maxSortOrder ?? 0) + 1;
    }

    private async Task NormalizeCategorySortOrdersAsync(CancellationToken cancellationToken)
    {
        var changed = false;
        var behaviorCategories = await _dbContext.BehaviorScoreCategories
            .OrderBy(x => x.SortOrder > 0 ? 0 : 1)
            .ThenBy(x => x.SortOrder)
            .ThenBy(x => x.Id)
            .ToListAsync(cancellationToken);

        for (var index = 0; index < behaviorCategories.Count; index++)
        {
            var sortOrder = index + 1;
            if (behaviorCategories[index].SortOrder != sortOrder)
            {
                behaviorCategories[index].SortOrder = sortOrder;
                changed = true;
            }
        }

        var goodnessCategories = await _dbContext.GoodnessBankCategories
            .OrderBy(x => x.SortOrder > 0 ? 0 : 1)
            .ThenBy(x => x.SortOrder)
            .ThenBy(x => x.Id)
            .ToListAsync(cancellationToken);

        for (var index = 0; index < goodnessCategories.Count; index++)
        {
            var sortOrder = index + 1;
            if (goodnessCategories[index].SortOrder != sortOrder)
            {
                goodnessCategories[index].SortOrder = sortOrder;
                changed = true;
            }
        }

        if (changed)
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    private async Task<StudentCareHomeroomTeacherPageViewModel> BuildHomeroomTeachersViewModelAsync(CancellationToken cancellationToken)
    {
        var homeroomUsers = await _userManager.GetUsersInRoleAsync(HomeroomRoleName);
        var userIds = homeroomUsers.Select(x => x.Id).ToList();
        var users = await _dbContext.Users
            .AsNoTracking()
            .Include(x => x.AssignedClassroom)
            .ThenInclude(x => x!.GradeLevel)
            .Where(x => userIds.Contains(x.Id))
            .OrderBy(x => x.AssignedClassroom!.GradeLevel!.SortOrder)
            .ThenBy(x => x.AssignedClassroom!.Name)
            .ThenBy(x => x.FullName)
            .ToListAsync(cancellationToken);

        var classroomOptions = await _dbContext.Classrooms
            .AsNoTracking()
            .Include(x => x.GradeLevel)
            .Where(x => x.IsActive)
            .OrderBy(x => x.GradeLevel!.SortOrder)
            .ThenBy(x => x.Name)
            .ToListAsync(cancellationToken);

        return new StudentCareHomeroomTeacherPageViewModel
        {
            Teachers = users.Select(x => new StudentCareHomeroomTeacherListItemViewModel
            {
                UserId = x.Id,
                Username = x.UserName ?? string.Empty,
                Email = x.Email ?? string.Empty,
                FullName = x.FullName,
                AssignedClassroomId = x.AssignedClassroomId,
                AssignedClassroomName = x.AssignedClassroom is null
                    ? "-"
                    : FormatClassroomDisplay(x.AssignedClassroom.GradeLevel?.Name, x.AssignedClassroom.Name),
                IsActive = x.IsActive
            }).ToList(),
            ClassroomOptions = classroomOptions.Select(x => new StudentCareClassroomOptionViewModel
            {
                Value = x.Id,
                Text = FormatClassroomDisplay(x.GradeLevel?.Name, x.Name),
                GradeLevelId = x.GradeLevelId
            }).ToList()
        };
    }

    private static string FormatClassroomDisplay(string? gradeName, string? classroomName)
    {
        var grade = (gradeName ?? string.Empty).Trim();
        var room = (classroomName ?? string.Empty).Trim();

        if (string.IsNullOrWhiteSpace(grade))
        {
            return string.IsNullOrWhiteSpace(room) ? "-" : room;
        }

        if (string.IsNullOrWhiteSpace(room))
        {
            return grade;
        }

        room = room.Replace("ห้อง", string.Empty, StringComparison.OrdinalIgnoreCase).Trim();
        if (room.Contains('/'))
        {
            room = room.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).LastOrDefault() ?? room;
        }

        return string.IsNullOrWhiteSpace(room) ? grade : $"{grade}/{room}";
    }

    private Task LogAsync(string action, int id, string name, CancellationToken cancellationToken)
    {
        return _auditLogService.LogAsync(
            User.FindFirstValue(ClaimTypes.NameIdentifier),
            action,
            "StudentCareCategory",
            id.ToString(),
            name,
            HttpContextHelper.GetIpAddress(HttpContext),
            cancellationToken);
    }

    private Task LogUserAsync(string action, string id, string name, CancellationToken cancellationToken)
    {
        return _auditLogService.LogAsync(
            User.FindFirstValue(ClaimTypes.NameIdentifier),
            action,
            "StudentCareHomeroomTeacher",
            id,
            name,
            HttpContextHelper.GetIpAddress(HttpContext),
            cancellationToken);
    }
}
