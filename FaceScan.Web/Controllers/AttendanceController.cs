using FaceScan.Web.Services.Interfaces;
using FaceScan.Web.ViewModels.Attendance;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FaceScan.Web.Controllers;

[Authorize(Roles = "SuperAdmin,Admin,Staff,Viewer,Executive,Teacher,HomeroomHead")]
public class AttendanceController : Controller
{
    private readonly IAttendanceReportService _attendanceReportService;

    public AttendanceController(IAttendanceReportService attendanceReportService)
    {
        _attendanceReportService = attendanceReportService;
    }

    [HttpGet]
    public async Task<IActionResult> Index([FromQuery] AttendanceFilterViewModel filter, CancellationToken cancellationToken)
    {
        filter.Date ??= DateTime.Today;
        var model = await _attendanceReportService.GetTransactionsAsync(filter, cancellationToken);
        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> Daily([FromQuery] AttendanceFilterViewModel filter, CancellationToken cancellationToken)
    {
        filter.Date ??= DateTime.Today;
        var model = await _attendanceReportService.GetDailyAsync(filter, cancellationToken);
        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> Late([FromQuery] AttendanceFilterViewModel filter, CancellationToken cancellationToken)
    {
        filter.Date ??= DateTime.Today;
        var model = await _attendanceReportService.GetLateAsync(filter, cancellationToken);
        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> ByClass([FromQuery] AttendanceFilterViewModel filter, CancellationToken cancellationToken)
    {
        filter.Date ??= DateTime.Today;
        var model = await _attendanceReportService.GetByClassAsync(filter, cancellationToken);
        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> PrintDaily([FromQuery] AttendanceFilterViewModel filter, CancellationToken cancellationToken)
    {
        filter.Date ??= DateTime.Today;
        var model = await _attendanceReportService.GetDailyAsync(filter, cancellationToken);
        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> ExportDailyCsv([FromQuery] AttendanceFilterViewModel filter, CancellationToken cancellationToken)
    {
        filter.Date ??= DateTime.Today;
        var bytes = await _attendanceReportService.ExportDailyCsvAsync(filter, cancellationToken);
        return File(bytes, "text/csv", $"attendance_daily_{filter.Date:yyyyMMdd}.csv");
    }

    [HttpGet]
    public async Task<IActionResult> ExportLateCsv([FromQuery] AttendanceFilterViewModel filter, CancellationToken cancellationToken)
    {
        filter.Date ??= DateTime.Today;
        var bytes = await _attendanceReportService.ExportLateCsvAsync(filter, cancellationToken);
        return File(bytes, "text/csv", $"attendance_late_{filter.Date:yyyyMMdd}.csv");
    }

    [HttpGet]
    public async Task<IActionResult> ExportByClassCsv([FromQuery] AttendanceFilterViewModel filter, CancellationToken cancellationToken)
    {
        filter.Date ??= DateTime.Today;
        var bytes = await _attendanceReportService.ExportByClassCsvAsync(filter, cancellationToken);
        return File(bytes, "text/csv", $"attendance_by_class_{filter.Date:yyyyMMdd}.csv");
    }
}
