using FaceScan.Web.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FaceScan.Web.Controllers;

[Authorize(Roles = "SuperAdmin,Admin,Staff,Viewer,Executive,Teacher,HomeroomHead")]
public class DashboardController : Controller
{
    private readonly IDashboardService _dashboardService;

    public DashboardController(IDashboardService dashboardService)
    {
        _dashboardService = dashboardService;
    }

    public async Task<IActionResult> Index(DateTime? date, CancellationToken cancellationToken)
    {
        var model = await _dashboardService.GetDashboardAsync(date, cancellationToken);
        return View(model);
    }
}
