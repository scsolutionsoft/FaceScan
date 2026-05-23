using FaceScan.Web.Data;
using FaceScan.Web.Models.Entities;
using FaceScan.Web.ViewModels;
using FaceScan.Web.ViewModels.Users;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;

namespace FaceScan.Web.Controllers;

[Authorize(Roles = "SuperAdmin,Admin")]
public class UsersController : Controller
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<IdentityRole> _roleManager;
    private readonly ApplicationDbContext _dbContext;

    public UsersController(
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole> roleManager,
        ApplicationDbContext dbContext)
    {
        _userManager = userManager;
        _roleManager = roleManager;
        _dbContext = dbContext;
    }

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var users = await _dbContext.Users
            .AsNoTracking()
            .Include(x => x.AssignedClassroom)
            .OrderBy(x => x.UserName)
            .ToListAsync(cancellationToken);

        var userList = new List<UserListItemViewModel>();
        foreach (var user in users)
        {
            var role = (await _userManager.GetRolesAsync(user)).FirstOrDefault() ?? "-";
            userList.Add(new UserListItemViewModel
            {
                Id = user.Id,
                Username = user.UserName ?? string.Empty,
                Email = user.Email ?? string.Empty,
                FullName = user.FullName,
                Role = role,
                AssignedClassroom = user.AssignedClassroom?.Name,
                IsActive = user.IsActive
            });
        }

        ViewBag.Roles = await _roleManager.Roles
            .OrderBy(x => x.Name)
            .Select(x => x.Name!)
            .ToListAsync(cancellationToken);

        var classrooms = await GetClassroomOptionsAsync(cancellationToken);

        return View(new UsersIndexViewModel
        {
            Users = userList,
            CreateUser = new CreateUserViewModel(),
            Classrooms = classrooms
        });
    }

    [HttpGet]
    public async Task<IActionResult> Edit(string id, CancellationToken cancellationToken)
    {
        var user = await _dbContext.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (user is null)
        {
            TempData["Error"] = "ไม่พบผู้ใช้ที่ต้องการ";
            return RedirectToAction(nameof(Index));
        }

        var role = (await _userManager.GetRolesAsync(user)).FirstOrDefault() ?? "Viewer";
        ViewBag.Roles = await _roleManager.Roles
            .OrderBy(x => x.Name)
            .Select(x => x.Name!)
            .ToListAsync(cancellationToken);
        ViewBag.Classrooms = await GetClassroomOptionsAsync(cancellationToken);

        return View(new UpdateUserViewModel
        {
            Id = user.Id,
            Username = user.UserName ?? string.Empty,
            Email = user.Email ?? string.Empty,
            FullName = user.FullName,
            Role = role,
            IsActive = user.IsActive,
            AssignedClassroomId = user.AssignedClassroomId
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(UpdateUserViewModel model, CancellationToken cancellationToken)
    {
        ViewBag.Roles = await _roleManager.Roles
            .OrderBy(x => x.Name)
            .Select(x => x.Name!)
            .ToListAsync(cancellationToken);
        ViewBag.Classrooms = await GetClassroomOptionsAsync(cancellationToken);

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        if (!await _roleManager.RoleExistsAsync(model.Role))
        {
            ModelState.AddModelError(nameof(model.Role), "บทบาทไม่ถูกต้อง");
            return View(model);
        }

        var user = await _userManager.FindByIdAsync(model.Id);
        if (user is null)
        {
            TempData["Error"] = "ไม่พบผู้ใช้ที่ต้องการ";
            return RedirectToAction(nameof(Index));
        }

        var currentUsername = user.UserName ?? string.Empty;
        if (!string.Equals(currentUsername, model.Username, StringComparison.OrdinalIgnoreCase))
        {
            var duplicate = await _userManager.FindByNameAsync(model.Username);
            if (duplicate is not null)
            {
                ModelState.AddModelError(nameof(model.Username), "ชื่อผู้ใช้นี้ถูกใช้งานแล้ว");
                return View(model);
            }
        }

        user.UserName = model.Username.Trim();
        user.Email = model.Email.Trim();
        user.FullName = model.FullName.Trim();
        user.IsActive = model.IsActive;
        user.AssignedClassroomId = model.AssignedClassroomId;

        var updateResult = await _userManager.UpdateAsync(user);
        if (!updateResult.Succeeded)
        {
            foreach (var error in updateResult.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }

            return View(model);
        }

        var oldRoles = await _userManager.GetRolesAsync(user);
        if (!oldRoles.Contains(model.Role, StringComparer.OrdinalIgnoreCase))
        {
            if (oldRoles.Count > 0)
            {
                await _userManager.RemoveFromRolesAsync(user, oldRoles);
            }

            await _userManager.AddToRoleAsync(user, model.Role);
        }

        if (!string.IsNullOrWhiteSpace(model.NewPassword))
        {
            var token = await _userManager.GeneratePasswordResetTokenAsync(user);
            var resetResult = await _userManager.ResetPasswordAsync(user, token, model.NewPassword.Trim());
            if (!resetResult.Succeeded)
            {
                foreach (var error in resetResult.Errors)
                {
                    ModelState.AddModelError(nameof(model.NewPassword), error.Description);
                }

                return View(model);
            }
        }

        TempData["Success"] = "อัปเดตข้อมูลผู้ใช้เรียบร้อย";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateUserViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            TempData["Error"] = "ข้อมูลผู้ใช้ไม่ถูกต้อง";
            return RedirectToAction(nameof(Index));
        }

        if (!await _roleManager.RoleExistsAsync(model.Role))
        {
            TempData["Error"] = "บทบาทไม่ถูกต้อง";
            return RedirectToAction(nameof(Index));
        }

        var exists = await _userManager.FindByNameAsync(model.Username);
        if (exists is not null)
        {
            TempData["Error"] = "ชื่อผู้ใช้นี้มีอยู่แล้ว";
            return RedirectToAction(nameof(Index));
        }

        var user = new ApplicationUser
        {
            UserName = model.Username.Trim(),
            Email = model.Email.Trim(),
            FullName = model.FullName.Trim(),
            IsActive = true,
            EmailConfirmed = true,
            AssignedClassroomId = model.AssignedClassroomId
        };

        var createResult = await _userManager.CreateAsync(user, model.Password);
        if (!createResult.Succeeded)
        {
            TempData["Error"] = string.Join("; ", createResult.Errors.Select(x => x.Description));
            return RedirectToAction(nameof(Index));
        }

        await _userManager.AddToRoleAsync(user, model.Role);
        TempData["Success"] = "เพิ่มผู้ใช้เรียบร้อย";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleActive(string id, CancellationToken cancellationToken)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user is null)
        {
            TempData["Error"] = "ไม่พบผู้ใช้ที่ต้องการ";
            return RedirectToAction(nameof(Index));
        }

        user.IsActive = !user.IsActive;
        await _userManager.UpdateAsync(user);

        TempData["Success"] = "อัปเดตสถานะผู้ใช้เรียบร้อย";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetPassword(string id, string? returnUrl)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user is null)
        {
            TempData["Error"] = "ไม่พบผู้ใช้ที่ต้องการ";
            return RedirectToLocalOrIndex(returnUrl);
        }

        var temporaryPassword = GenerateTemporaryPassword();
        var token = await _userManager.GeneratePasswordResetTokenAsync(user);
        var resetResult = await _userManager.ResetPasswordAsync(user, token, temporaryPassword);
        if (!resetResult.Succeeded)
        {
            TempData["Error"] = string.Join("; ", resetResult.Errors.Select(x => x.Description));
            return RedirectToLocalOrIndex(returnUrl);
        }

        await _userManager.SetLockoutEndDateAsync(user, null);
        await _userManager.ResetAccessFailedCountAsync(user);

        TempData["Success"] = $"รีเซ็ตรหัสผ่านของ {user.UserName} เรียบร้อย รหัสผ่านชั่วคราวคือ: {temporaryPassword}";
        return RedirectToLocalOrIndex(returnUrl);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(string id, string? returnUrl, CancellationToken cancellationToken)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user is null)
        {
            TempData["Error"] = "ไม่พบผู้ใช้ที่ต้องการ";
            return RedirectToLocalOrIndex(returnUrl);
        }

        var currentUserId = _userManager.GetUserId(User);
        if (string.Equals(user.Id, currentUserId, StringComparison.Ordinal))
        {
            TempData["Error"] = "ไม่สามารถลบบัญชีที่กำลังใช้งานอยู่ได้";
            return RedirectToLocalOrIndex(returnUrl);
        }

        var roles = await _userManager.GetRolesAsync(user);
        if (roles.Contains("SuperAdmin", StringComparer.OrdinalIgnoreCase))
        {
            if (!User.IsInRole("SuperAdmin"))
            {
                TempData["Error"] = "มีเพียงผู้ดูแลสูงสุดเท่านั้นที่ลบบัญชีผู้ดูแลสูงสุดได้";
                return RedirectToLocalOrIndex(returnUrl);
            }

            var superAdminCount = await (
                from userRole in _dbContext.UserRoles
                join role in _dbContext.Roles on userRole.RoleId equals role.Id
                where role.Name == "SuperAdmin"
                select userRole.UserId)
                .Distinct()
                .CountAsync(cancellationToken);

            if (superAdminCount <= 1)
            {
                TempData["Error"] = "ไม่สามารถลบผู้ดูแลสูงสุดคนสุดท้ายของระบบได้";
                return RedirectToLocalOrIndex(returnUrl);
            }
        }

        var hasPeriodAttendanceRecords = await _dbContext.PeriodAttendanceSessions
            .AsNoTracking()
            .AnyAsync(x => x.CheckedByUserId == user.Id, cancellationToken);

        if (hasPeriodAttendanceRecords)
        {
            TempData["Error"] = "ไม่สามารถลบผู้ใช้รายนี้ได้ เนื่องจากยังมีข้อมูลการเช็กคาบเรียนที่อ้างอิงอยู่";
            return RedirectToLocalOrIndex(returnUrl);
        }

        var deleteResult = await _userManager.DeleteAsync(user);
        if (!deleteResult.Succeeded)
        {
            TempData["Error"] = string.Join("; ", deleteResult.Errors.Select(x => x.Description));
            return RedirectToLocalOrIndex(returnUrl);
        }

        TempData["Success"] = $"ลบข้อมูลผู้ใช้ {user.UserName} เรียบร้อย";
        return RedirectToAction(nameof(Index));
    }

    private async Task<IReadOnlyList<SelectOptionViewModel>> GetClassroomOptionsAsync(CancellationToken cancellationToken)
    {
        return await _dbContext.Classrooms
            .AsNoTracking()
            .Include(x => x.GradeLevel)
            .Where(x => x.IsActive)
            .OrderBy(x => x.GradeLevel!.SortOrder)
            .ThenBy(x => x.Name)
            .Select(x => new SelectOptionViewModel
            {
                Value = x.Id,
                Text = $"{x.GradeLevel!.Name}/{x.Name}"
            })
            .ToListAsync(cancellationToken);
    }

    private IActionResult RedirectToLocalOrIndex(string? returnUrl)
    {
        if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
        {
            return Redirect(returnUrl);
        }

        return RedirectToAction(nameof(Index));
    }

    private static string GenerateTemporaryPassword()
    {
        const string uppercase = "ABCDEFGHJKLMNPQRSTUVWXYZ";
        const string lowercase = "abcdefghijkmnopqrstuvwxyz";
        const string digits = "23456789";
        const string symbols = "!@#$%";
        var chars = new List<char>
        {
            GetRandomChar(uppercase),
            GetRandomChar(lowercase),
            GetRandomChar(digits),
            GetRandomChar(symbols)
        };

        var allChars = $"{uppercase}{lowercase}{digits}{symbols}";
        while (chars.Count < 12)
        {
            chars.Add(GetRandomChar(allChars));
        }

        for (var index = chars.Count - 1; index > 0; index--)
        {
            var swapIndex = RandomNumberGenerator.GetInt32(index + 1);
            (chars[index], chars[swapIndex]) = (chars[swapIndex], chars[index]);
        }

        return new string(chars.ToArray());
    }

    private static char GetRandomChar(string source)
        => source[RandomNumberGenerator.GetInt32(source.Length)];
}
