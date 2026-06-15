using FaceScan.Web.Data;
using FaceScan.Web.Models.Entities;
using FaceScan.Web.Services.Interfaces;
using FaceScan.Web.ViewModels.Teacher;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FaceScan.Web.Areas.Teacher.Controllers;

[Area("Teacher")]
[Authorize(Roles = "SuperAdmin,Admin,Teacher,HomeroomHead,StudentCareAdmin,WasteBankStaff")]
public class HomeController : Controller
{
    private readonly ApplicationDbContext _dbContext;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ISystemSettingService _systemSettingService;

    public HomeController(
        ApplicationDbContext dbContext,
        UserManager<ApplicationUser> userManager,
        ISystemSettingService systemSettingService)
    {
        _dbContext = dbContext;
        _userManager = userManager;
        _systemSettingService = systemSettingService;
    }

    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null)
        {
            return Challenge();
        }

        var roles = await _userManager.GetRolesAsync(user);
        var settings = await _systemSettingService.GetSettingsAsync(cancellationToken);
        var roleName = roles.FirstOrDefault(x => x is "Teacher" or "HomeroomHead" or "StudentCareAdmin" or "WasteBankStaff")
            ?? roles.FirstOrDefault()
            ?? "Teacher";

        var assignedClassroomName = "-";
        var assignedStudentCount = 0;

        if (user.AssignedClassroomId.HasValue)
        {
            assignedClassroomName = await _dbContext.Classrooms
                .AsNoTracking()
                .Where(x => x.Id == user.AssignedClassroomId.Value)
                .Select(x => x.Name)
                .FirstOrDefaultAsync(cancellationToken)
                ?? "-";

            assignedStudentCount = await _dbContext.Students
                .AsNoTracking()
                .CountAsync(x => x.ClassroomId == user.AssignedClassroomId.Value && x.IsActive, cancellationToken);
        }

        var model = new TeacherWorkspaceViewModel
        {
            DisplayName = string.IsNullOrWhiteSpace(user.FullName) ? (user.UserName ?? "Teacher") : user.FullName,
            RoleName = roleName,
            AssignedClassroomName = assignedClassroomName,
            AssignedStudentCount = assignedStudentCount,
            Today = DateTime.Today,
            CanManageTeacherFaces = User.IsInRole("SuperAdmin") || User.IsInRole("Admin"),
            CanViewTeacherReports = User.IsInRole("SuperAdmin") || User.IsInRole("Admin"),
            CanUseTeacherScan = User.IsInRole("SuperAdmin") || User.IsInRole("Admin") || User.IsInRole("Teacher") || User.IsInRole("HomeroomHead") || User.IsInRole("Staff"),
            CanUsePeriodAttendance = User.IsInRole("SuperAdmin") || User.IsInRole("Admin") || User.IsInRole("Teacher") || User.IsInRole("HomeroomHead"),
            CanUseStudentCare = settings.EnableStudentCareModule && (User.IsInRole("SuperAdmin") || User.IsInRole("Admin") || User.IsInRole("Teacher") || User.IsInRole("HomeroomHead") || User.IsInRole("StudentCareAdmin")),
            CanUseWasteBank = settings.EnableWasteBankModule && (User.IsInRole("SuperAdmin") || User.IsInRole("Admin") || User.IsInRole("WasteBankStaff"))
        };

        return View(model);
    }
}
