using FaceScan.Web.Data;
using FaceScan.Web.Helpers;
using FaceScan.Web.Models.Entities;
using FaceScan.Web.Services.Interfaces;
using FaceScan.Web.ViewModels.Students;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FaceScan.Web.Controllers;

[Authorize(Roles = "SuperAdmin,Admin")]
public class StudentGuardiansController : Controller
{
    private static readonly string[] DefaultRelationships = ["บิดา", "มารดา", "ผู้ปกครอง", "อื่นๆ"];

    private readonly ApplicationDbContext _dbContext;
    private readonly IFileStorageService _fileStorageService;
    private readonly IAuditLogService _auditLogService;

    public StudentGuardiansController(
        ApplicationDbContext dbContext,
        IFileStorageService fileStorageService,
        IAuditLogService auditLogService)
    {
        _dbContext = dbContext;
        _fileStorageService = fileStorageService;
        _auditLogService = auditLogService;
    }

    [HttpGet]
    public async Task<IActionResult> Index(int studentId, CancellationToken cancellationToken)
    {
        var student = await GetStudentAsync(studentId, cancellationToken);
        if (student is null)
        {
            return NotFound();
        }

        var guardians = await _dbContext.StudentGuardians
            .AsNoTracking()
            .Where(x => x.StudentId == student.Id)
            .OrderByDescending(x => x.IsPrimaryContact)
            .ThenBy(x => x.Relationship)
            .ThenBy(x => x.FullName)
            .Select(x => new StudentGuardianListItemViewModel
            {
                Id = x.Id,
                FullName = x.FullName,
                NationalId = x.NationalId,
                Relationship = x.Relationship,
                PhoneNumber = x.PhoneNumber,
                Address = x.Address,
                PhotoPath = x.PhotoPath,
                IsPrimaryContact = x.IsPrimaryContact
            })
            .ToListAsync(cancellationToken);

        return View(new StudentGuardianIndexViewModel
        {
            StudentId = student.Id,
            StudentCode = student.StudentCode,
            StudentName = student.FullName,
            Classroom = student.Classroom?.Name ?? "-",
            Guardians = guardians
        });
    }

    [HttpGet]
    public async Task<IActionResult> Create(int studentId, CancellationToken cancellationToken)
    {
        var student = await GetStudentAsync(studentId, cancellationToken);
        if (student is null)
        {
            return NotFound();
        }

        var model = new StudentGuardianFormViewModel
        {
            StudentId = student.Id,
            StudentCode = student.StudentCode,
            StudentName = student.FullName,
            Classroom = student.Classroom?.Name ?? "-",
            Relationship = "ผู้ปกครอง",
            IsPrimaryContact = !await _dbContext.StudentGuardians.AnyAsync(x => x.StudentId == student.Id, cancellationToken)
        };

        await PopulateFormAsync(model, null, cancellationToken);
        return View("Form", model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(StudentGuardianFormViewModel model, CancellationToken cancellationToken)
    {
        var student = await GetStudentAsync(model.StudentId, cancellationToken);
        if (student is null)
        {
            return NotFound();
        }

        if (!ModelState.IsValid)
        {
            await PopulateStudentFieldsAsync(model, student, cancellationToken);
            await PopulateFormAsync(model, null, cancellationToken);
            return View("Form", model);
        }

        string? photoPath = null;
        try
        {
            if (model.Photo is { Length: > 0 })
            {
                photoPath = await _fileStorageService.SaveStudentCarePhotoAsync(model.Photo, student.Id, "guardian", cancellationToken);
            }
        }
        catch (InvalidOperationException ex)
        {
            ModelState.AddModelError(nameof(model.Photo), ex.Message);
            await PopulateStudentFieldsAsync(model, student, cancellationToken);
            await PopulateFormAsync(model, null, cancellationToken);
            return View("Form", model);
        }

        var guardian = new StudentGuardian
        {
            StudentId = student.Id,
            FullName = model.FullName.Trim(),
            NationalId = model.NationalId?.Trim(),
            Relationship = model.Relationship?.Trim(),
            PhoneNumber = model.PhoneNumber?.Trim(),
            Occupation = model.Occupation?.Trim(),
            MonthlyIncome = model.MonthlyIncome,
            Address = model.Address?.Trim(),
            PhotoPath = photoPath,
            IsPrimaryContact = model.IsPrimaryContact
        };

        _dbContext.StudentGuardians.Add(guardian);
        if (guardian.IsPrimaryContact)
        {
            await ClearOtherPrimaryContactsAsync(student.Id, null, cancellationToken);
            SyncStudentPrimaryGuardian(student, guardian);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        await LogAsync("CreateStudentGuardian", guardian.Id, $"{student.StudentCode} {guardian.FullName}", cancellationToken);

        TempData["Success"] = "บันทึกข้อมูลครอบครัวเรียบร้อย";
        return RedirectToAction(nameof(Index), new { studentId = student.Id });
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id, CancellationToken cancellationToken)
    {
        var guardian = await _dbContext.StudentGuardians
            .Include(x => x.Student)!.ThenInclude(x => x!.Classroom)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (guardian is null || guardian.Student is null)
        {
            return NotFound();
        }

        var model = new StudentGuardianFormViewModel
        {
            Id = guardian.Id,
            StudentId = guardian.StudentId,
            StudentCode = guardian.Student.StudentCode,
            StudentName = guardian.Student.FullName,
            Classroom = guardian.Student.Classroom?.Name ?? "-",
            FullName = guardian.FullName,
            NationalId = guardian.NationalId,
            Relationship = guardian.Relationship,
            PhoneNumber = guardian.PhoneNumber,
            Occupation = guardian.Occupation,
            MonthlyIncome = guardian.MonthlyIncome,
            Address = guardian.Address,
            CurrentPhotoPath = guardian.PhotoPath,
            IsPrimaryContact = guardian.IsPrimaryContact
        };

        await PopulateFormAsync(model, guardian.Id, cancellationToken);
        return View("Form", model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(StudentGuardianFormViewModel model, CancellationToken cancellationToken)
    {
        if (!model.Id.HasValue)
        {
            return BadRequest();
        }

        var guardian = await _dbContext.StudentGuardians
            .Include(x => x.Student)
            .FirstOrDefaultAsync(x => x.Id == model.Id.Value && x.StudentId == model.StudentId, cancellationToken);
        if (guardian is null || guardian.Student is null)
        {
            return NotFound();
        }

        if (!ModelState.IsValid)
        {
            await PopulateStudentFieldsAsync(model, guardian.Student, cancellationToken);
            await PopulateFormAsync(model, guardian.Id, cancellationToken);
            return View("Form", model);
        }

        var oldPhotoPath = guardian.PhotoPath;
        try
        {
            if (model.Photo is { Length: > 0 })
            {
                guardian.PhotoPath = await _fileStorageService.SaveStudentCarePhotoAsync(model.Photo, guardian.StudentId, "guardian", cancellationToken);
            }
        }
        catch (InvalidOperationException ex)
        {
            ModelState.AddModelError(nameof(model.Photo), ex.Message);
            await PopulateStudentFieldsAsync(model, guardian.Student, cancellationToken);
            await PopulateFormAsync(model, guardian.Id, cancellationToken);
            return View("Form", model);
        }

        guardian.FullName = model.FullName.Trim();
        guardian.NationalId = model.NationalId?.Trim();
        guardian.Relationship = model.Relationship?.Trim();
        guardian.PhoneNumber = model.PhoneNumber?.Trim();
        guardian.Occupation = model.Occupation?.Trim();
        guardian.MonthlyIncome = model.MonthlyIncome;
        guardian.Address = model.Address?.Trim();
        guardian.IsPrimaryContact = model.IsPrimaryContact;

        if (guardian.IsPrimaryContact)
        {
            await ClearOtherPrimaryContactsAsync(guardian.StudentId, guardian.Id, cancellationToken);
            SyncStudentPrimaryGuardian(guardian.Student, guardian);
        }
        else if (!await _dbContext.StudentGuardians.AnyAsync(x => x.StudentId == guardian.StudentId && x.Id != guardian.Id && x.IsPrimaryContact, cancellationToken))
        {
            SyncStudentPrimaryGuardian(guardian.Student, null);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        if (!string.IsNullOrWhiteSpace(oldPhotoPath) && !string.Equals(oldPhotoPath, guardian.PhotoPath, StringComparison.OrdinalIgnoreCase))
        {
            await _fileStorageService.DeleteFileIfExistsAsync(oldPhotoPath);
        }

        await LogAsync("UpdateStudentGuardian", guardian.Id, $"{guardian.Student.StudentCode} {guardian.FullName}", cancellationToken);
        TempData["Success"] = "แก้ไขข้อมูลครอบครัวเรียบร้อย";
        return RedirectToAction(nameof(Index), new { studentId = guardian.StudentId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var guardian = await _dbContext.StudentGuardians
            .Include(x => x.Student)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (guardian is null || guardian.Student is null)
        {
            return NotFound();
        }

        var studentId = guardian.StudentId;
        var photoPath = guardian.PhotoPath;
        var wasPrimary = guardian.IsPrimaryContact;
        _dbContext.StudentGuardians.Remove(guardian);

        if (wasPrimary)
        {
            SyncStudentPrimaryGuardian(guardian.Student, null);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        if (!string.IsNullOrWhiteSpace(photoPath))
        {
            await _fileStorageService.DeleteFileIfExistsAsync(photoPath);
        }

        await LogAsync("DeleteStudentGuardian", id, $"{guardian.Student.StudentCode} {guardian.FullName}", cancellationToken);
        TempData["Success"] = "ลบข้อมูลครอบครัวเรียบร้อย";
        return RedirectToAction(nameof(Index), new { studentId });
    }

    private async Task<Student?> GetStudentAsync(int studentId, CancellationToken cancellationToken)
    {
        return await _dbContext.Students
            .Include(x => x.Classroom)
            .FirstOrDefaultAsync(x => x.Id == studentId && x.IsActive, cancellationToken);
    }

    private async Task PopulateStudentFieldsAsync(StudentGuardianFormViewModel model, Student student, CancellationToken cancellationToken)
    {
        model.StudentCode = student.StudentCode;
        model.StudentName = student.FullName;
        model.Classroom = await _dbContext.Classrooms
            .AsNoTracking()
            .Where(x => x.Id == student.ClassroomId)
            .Select(x => x.Name)
            .FirstOrDefaultAsync(cancellationToken) ?? "-";
    }

    private async Task PopulateFormAsync(StudentGuardianFormViewModel model, int? currentGuardianId, CancellationToken cancellationToken)
    {
        ViewBag.Relationships = DefaultRelationships;
        model.CopySources = await _dbContext.StudentGuardians
            .AsNoTracking()
            .Where(x => x.StudentId == model.StudentId && x.Id != currentGuardianId)
            .Where(x => x.Relationship == "บิดา" || x.Relationship == "มารดา")
            .OrderBy(x => x.Relationship)
            .ThenBy(x => x.FullName)
            .Select(x => new StudentGuardianCopySourceViewModel
            {
                Id = x.Id,
                Label = $"{x.Relationship}: {x.FullName}",
                FullName = x.FullName,
                NationalId = x.NationalId,
                Relationship = x.Relationship,
                PhoneNumber = x.PhoneNumber,
                Occupation = x.Occupation,
                MonthlyIncome = x.MonthlyIncome,
                Address = x.Address,
                PhotoPath = x.PhotoPath
            })
            .ToListAsync(cancellationToken);
    }

    private async Task ClearOtherPrimaryContactsAsync(int studentId, int? exceptGuardianId, CancellationToken cancellationToken)
    {
        var others = await _dbContext.StudentGuardians
            .Where(x => x.StudentId == studentId && x.IsPrimaryContact)
            .Where(x => !exceptGuardianId.HasValue || x.Id != exceptGuardianId.Value)
            .ToListAsync(cancellationToken);

        foreach (var other in others)
        {
            other.IsPrimaryContact = false;
        }
    }

    private static void SyncStudentPrimaryGuardian(Student student, StudentGuardian? guardian)
    {
        student.GuardianName = guardian?.FullName;
        student.GuardianNationalId = guardian?.NationalId;
        student.GuardianPhone = guardian?.PhoneNumber;
    }

    private Task LogAsync(string action, int entityId, string description, CancellationToken cancellationToken)
    {
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        return _auditLogService.LogAsync(
            userId,
            action,
            "StudentGuardian",
            entityId.ToString(),
            description,
            HttpContextHelper.GetIpAddress(HttpContext),
            cancellationToken);
    }
}
