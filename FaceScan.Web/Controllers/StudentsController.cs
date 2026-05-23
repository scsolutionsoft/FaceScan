using FaceScan.Web.Helpers;
using FaceScan.Web.Services.Interfaces;
using FaceScan.Web.ViewModels.Students;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FaceScan.Web.Controllers;

[Authorize(Roles = "SuperAdmin,Admin")]
public class StudentsController : Controller
{
    private readonly IStudentService _studentService;

    public StudentsController(IStudentService studentService)
    {
        _studentService = studentService;
    }

    public async Task<IActionResult> Index([FromQuery] StudentFilterViewModel filter, CancellationToken cancellationToken)
    {
        var result = await _studentService.GetStudentsAsync(filter, cancellationToken);
        var model = new StudentIndexViewModel
        {
            Filter = filter,
            Students = result.Items,
            TotalCount = result.TotalCount,
            AcademicYears = await _studentService.GetAcademicYearOptionsAsync(cancellationToken),
            GradeLevels = await _studentService.GetGradeLevelOptionsAsync(cancellationToken),
            Classrooms = await _studentService.GetClassroomOptionsAsync(cancellationToken)
        };

        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> Create(CancellationToken cancellationToken)
    {
        await PopulateSelectListsAsync(cancellationToken);
        return View(new StudentUpsertViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(StudentUpsertViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            await PopulateSelectListsAsync(cancellationToken);
            return View(model);
        }

        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        var ip = HttpContextHelper.GetIpAddress(HttpContext);
        var result = await _studentService.CreateAsync(model, userId, ip, cancellationToken);

        if (!result.Success)
        {
            ModelState.AddModelError(string.Empty, result.Message);
            await PopulateSelectListsAsync(cancellationToken);
            return View(model);
        }

        TempData["Success"] = result.Message;
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id, CancellationToken cancellationToken)
    {
        var model = await _studentService.GetForEditAsync(id, cancellationToken);
        if (model is null)
        {
            return NotFound();
        }

        await PopulateSelectListsAsync(cancellationToken);
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(StudentUpsertViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            await PopulateSelectListsAsync(cancellationToken);
            return View(model);
        }

        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        var ip = HttpContextHelper.GetIpAddress(HttpContext);

        var result = await _studentService.UpdateAsync(model, userId, ip, cancellationToken);
        if (!result.Success)
        {
            ModelState.AddModelError(string.Empty, result.Message);
            await PopulateSelectListsAsync(cancellationToken);
            return View(model);
        }

        TempData["Success"] = result.Message;
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Details(int id, CancellationToken cancellationToken)
    {
        var model = await _studentService.GetStudentDetailAsync(id, cancellationToken);
        if (model is null)
        {
            return NotFound();
        }

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        var ip = HttpContextHelper.GetIpAddress(HttpContext);

        var result = await _studentService.DeactivateAsync(id, userId, ip, cancellationToken);
        TempData[result.Success ? "Success" : "Error"] = result.Message;
        return RedirectToAction(nameof(Index));
    }

    private async Task PopulateSelectListsAsync(CancellationToken cancellationToken)
    {
        ViewBag.AcademicYears = await _studentService.GetAcademicYearOptionsAsync(cancellationToken);
        ViewBag.GradeLevels = await _studentService.GetGradeLevelOptionsAsync(cancellationToken);
        ViewBag.Classrooms = await _studentService.GetClassroomOptionsAsync(cancellationToken);
    }
}
