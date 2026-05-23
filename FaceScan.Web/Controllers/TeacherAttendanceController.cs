using FaceScan.Web.Services.Interfaces;
using FaceScan.Web.ViewModels.TeacherAttendance;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FaceScan.Web.Controllers;

[Authorize(Roles = "SuperAdmin,Admin,Executive")]
public class TeacherAttendanceController : Controller
{
    private readonly ITeacherAttendanceReportService _teacherAttendanceReportService;

    public TeacherAttendanceController(ITeacherAttendanceReportService teacherAttendanceReportService)
    {
        _teacherAttendanceReportService = teacherAttendanceReportService;
    }

    [HttpGet]
    public async Task<IActionResult> Index([FromQuery] TeacherAttendanceFilterViewModel filter, CancellationToken cancellationToken)
    {
        filter.Date ??= DateTime.Today;
        var model = await _teacherAttendanceReportService.GetTransactionsAsync(filter, cancellationToken);
        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> Daily([FromQuery] TeacherAttendanceFilterViewModel filter, CancellationToken cancellationToken)
    {
        filter.Date ??= DateTime.Today;
        var model = await _teacherAttendanceReportService.GetDailyAsync(filter, cancellationToken);
        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> Late([FromQuery] TeacherAttendanceFilterViewModel filter, CancellationToken cancellationToken)
    {
        filter.Date ??= DateTime.Today;
        var model = await _teacherAttendanceReportService.GetLateAsync(filter, cancellationToken);
        return View(model);
    }
}
