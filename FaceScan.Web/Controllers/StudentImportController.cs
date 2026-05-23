using FaceScan.Web.Helpers;
using FaceScan.Web.Services.Interfaces;
using FaceScan.Web.ViewModels.Import;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FaceScan.Web.Controllers;

[Authorize(Roles = "SuperAdmin,Admin")]
public class StudentImportController : Controller
{
    private readonly IStudentImportService _studentImportService;

    public StudentImportController(IStudentImportService studentImportService)
    {
        _studentImportService = studentImportService;
    }

    [HttpGet]
    public IActionResult Index()
    {
        return View(new StudentImportIndexViewModel());
    }

    [HttpGet]
    public IActionResult DownloadTemplate()
    {
        var bytes = _studentImportService.GenerateTemplateCsv();
        return File(bytes, "text/csv", "students_template.csv");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Preview(StudentImportUploadViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid || model.File is null)
        {
            TempData["Error"] = "กรุณาเลือกไฟล์ซีเอสวี";
            return RedirectToAction(nameof(Index));
        }

        try
        {
            var preview = await _studentImportService.PreviewAsync(model.File, cancellationToken);
            return View("Index", preview);
        }
        catch (Exception ex)
        {
            TempData["Error"] = ex.Message;
            return RedirectToAction(nameof(Index));
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Import(string previewToken, CancellationToken cancellationToken)
    {
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        var ip = HttpContextHelper.GetIpAddress(HttpContext);

        var result = await _studentImportService.ImportAsync(previewToken, userId, ip, cancellationToken);
        if (result.BatchId <= 0)
        {
            TempData["Error"] = string.Join("; ", result.Errors);
            return RedirectToAction(nameof(Index));
        }

        TempData["Success"] = $"นำเข้าสำเร็จ {result.SuccessRows} แถว, ผิดพลาด {result.FailedRows} แถว";
        if (result.Errors.Count > 0)
        {
            TempData["Error"] = string.Join("; ", result.Errors.Take(5));
        }

        return RedirectToAction(nameof(Index));
    }
}
