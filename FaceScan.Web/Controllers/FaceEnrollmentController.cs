using System.Security.Claims;
using FaceScan.Web.Data;
using FaceScan.Web.Helpers;
using FaceScan.Web.Models.Entities;
using FaceScan.Web.Models.Enums;
using FaceScan.Web.Services.Interfaces;
using FaceScan.Web.ViewModels.FaceEnrollment;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FaceScan.Web.Controllers;

[Authorize(Roles = "SuperAdmin,Admin")]
public class FaceEnrollmentController : Controller
{
    private static readonly HashSet<string> AllowedCapturedContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg",
        "image/png",
        "image/webp"
    };

    private readonly ApplicationDbContext _dbContext;
    private readonly IFileStorageService _fileStorageService;
    private readonly IFaceRecognitionService _faceRecognitionService;
    private readonly IAuditLogService _auditLogService;

    public FaceEnrollmentController(
        ApplicationDbContext dbContext,
        IFileStorageService fileStorageService,
        IFaceRecognitionService faceRecognitionService,
        IAuditLogService auditLogService)
    {
        _dbContext = dbContext;
        _fileStorageService = fileStorageService;
        _faceRecognitionService = faceRecognitionService;
        _auditLogService = auditLogService;
    }

    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var students = await _dbContext.Students
            .Include(x => x.FaceProfile)
            .Include(x => x.Classroom)
            .Include(x => x.StudentPhotos)
            .Where(x => x.IsActive)
            .AsNoTracking()
            .OrderBy(x => x.StudentCode)
            .Select(x => new FaceEnrollmentStudentViewModel
            {
                StudentId = x.Id,
                StudentCode = x.StudentCode,
                FullName = x.FullName,
                ClassroomName = x.Classroom != null ? x.Classroom.Name : "-",
                EnrollmentStatus = x.FaceProfile != null ? x.FaceProfile.EnrollmentStatus : EnrollmentStatus.NotRegistered,
                PhotoCount = x.StudentPhotos.Count
            })
            .ToListAsync(cancellationToken);

        return View(new FaceEnrollmentIndexViewModel { Students = students });
    }

    [HttpGet]
    public async Task<IActionResult> Upload(int studentId, CancellationToken cancellationToken)
    {
        var model = await BuildUploadViewModelAsync(studentId, cancellationToken);
        if (model is null)
        {
            return NotFound();
        }

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Upload(FaceUploadViewModel model, CancellationToken cancellationToken)
    {
        var student = await _dbContext.Students
            .Include(x => x.StudentPhotos)
            .FirstOrDefaultAsync(x => x.Id == model.StudentId && x.IsActive, cancellationToken);

        if (student is null)
        {
            return NotFound();
        }

        var uploadedFiles = (model.Files ?? [])
            .Where(x => x is { Length: > 0 })
            .ToList();
        var capturedFiles = ConvertCapturedImagesToFiles(model.CapturedImages ?? []);
        var incomingFiles = uploadedFiles.Concat(capturedFiles.Select(x => x.FormFile)).ToList();

        if (incomingFiles.Count == 0)
        {
            TempData["Error"] = "กรุณาอัปโหลดหรือถ่ายรูปอย่างน้อย 1 รูป";
            DisposeCapturedStreams(capturedFiles);
            return RedirectToAction(nameof(Upload), new { studentId = model.StudentId });
        }

        var hasPrimary = student.StudentPhotos.Any(x => x.IsPrimary);
        var addedCount = 0;
        var skippedCount = 0;

        try
        {
            foreach (var file in incomingFiles)
            {
                try
                {
                    var path = await _fileStorageService.SaveStudentPhotoAsync(file, model.StudentId, cancellationToken);
                    _dbContext.StudentPhotos.Add(new StudentPhoto
                    {
                        StudentId = model.StudentId,
                        FilePath = path,
                        FileName = Path.GetFileName(path),
                        ContentType = file.ContentType,
                        IsPrimary = !hasPrimary,
                        CapturedAt = DateTime.UtcNow,
                        QualityScore = 0.90m
                    });

                    hasPrimary = true;
                    addedCount++;
                }
                catch (InvalidOperationException)
                {
                    skippedCount++;
                }
            }

            if (addedCount == 0)
            {
                TempData["Error"] = "ไม่พบรูปที่ถูกต้องสำหรับเพิ่มเข้าระบบ";
                return RedirectToAction(nameof(Upload), new { studentId = model.StudentId });
            }

            await _dbContext.SaveChangesAsync(cancellationToken);

            var rebuildResult = await RebuildFaceProfileAsync(model.StudentId, cancellationToken);
            var message = rebuildResult.Message;
            if (skippedCount > 0)
            {
                message = $"{message} (ข้ามไฟล์ที่ไม่ถูกต้อง: {skippedCount})";
            }

            await LogAuditAsync("EnrollFace", model.StudentId, message, cancellationToken);
            TempData[rebuildResult.Success ? "Success" : "Error"] = message;
            return RedirectToAction(nameof(Upload), new { studentId = model.StudentId });
        }
        finally
        {
            DisposeCapturedStreams(capturedFiles);
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetPrimaryPhoto(int studentId, int photoId, CancellationToken cancellationToken)
    {
        var photos = await _dbContext.StudentPhotos
            .Where(x => x.StudentId == studentId)
            .OrderByDescending(x => x.IsPrimary)
            .ThenByDescending(x => x.CapturedAt)
            .ToListAsync(cancellationToken);

        if (photos.Count == 0)
        {
            TempData["Error"] = "ไม่พบรูปใบหน้าของนักเรียนคนนี้";
            return RedirectToAction(nameof(Upload), new { studentId });
        }

        var target = photos.FirstOrDefault(x => x.Id == photoId);
        if (target is null)
        {
            TempData["Error"] = "ไม่พบรูปที่ต้องการ";
            return RedirectToAction(nameof(Upload), new { studentId });
        }

        foreach (var photo in photos)
        {
            photo.IsPrimary = photo.Id == photoId;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        await LogAuditAsync("SetPrimaryFacePhoto", studentId, $"ตั้งรูปหลักเป็นรหัสรูป {photoId}", cancellationToken);

        TempData["Success"] = "ตั้งค่ารูปหลักเรียบร้อย";
        return RedirectToAction(nameof(Upload), new { studentId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeletePhoto(int studentId, int photoId, CancellationToken cancellationToken)
    {
        var photo = await _dbContext.StudentPhotos
            .FirstOrDefaultAsync(x => x.Id == photoId && x.StudentId == studentId, cancellationToken);

        if (photo is null)
        {
            TempData["Error"] = "ไม่พบรูปที่ต้องการ";
            return RedirectToAction(nameof(Upload), new { studentId });
        }

        var filePath = photo.FilePath;
        _dbContext.StudentPhotos.Remove(photo);
        await _dbContext.SaveChangesAsync(cancellationToken);
        await _fileStorageService.DeleteFileIfExistsAsync(filePath);

        await NormalizePrimaryPhotoAsync(studentId, cancellationToken);
        var rebuildResult = await RebuildFaceProfileAsync(studentId, cancellationToken);

        var message = $"ลบรูปใบหน้าเรียบร้อย {rebuildResult.Message}";
        await LogAuditAsync("DeleteFacePhoto", studentId, message, cancellationToken);
        TempData[rebuildResult.Success ? "Success" : "Error"] = message;
        return RedirectToAction(nameof(Upload), new { studentId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ClearEnrollment(int studentId, CancellationToken cancellationToken)
    {
        var student = await _dbContext.Students
            .Include(x => x.StudentPhotos)
            .FirstOrDefaultAsync(x => x.Id == studentId && x.IsActive, cancellationToken);

        if (student is null)
        {
            return NotFound();
        }

        var photoPaths = student.StudentPhotos.Select(x => x.FilePath).ToList();
        if (student.StudentPhotos.Count > 0)
        {
            _dbContext.StudentPhotos.RemoveRange(student.StudentPhotos);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        foreach (var path in photoPaths)
        {
            await _fileStorageService.DeleteFileIfExistsAsync(path);
        }

        await _faceRecognitionService.RemoveStudentProfileAsync(studentId);
        await LogAuditAsync("ClearFaceEnrollment", studentId, "ล้างข้อมูลลงทะเบียนใบหน้าเรียบร้อย", cancellationToken);

        TempData["Success"] = "รีเซ็ตการลงทะเบียนใบหน้าเรียบร้อย";
        return RedirectToAction(nameof(Upload), new { studentId });
    }

    private async Task<FaceUploadViewModel?> BuildUploadViewModelAsync(int studentId, CancellationToken cancellationToken)
    {
        return await _dbContext.Students
            .AsNoTracking()
            .Include(x => x.Classroom)
            .Include(x => x.FaceProfile)
            .Include(x => x.StudentPhotos)
            .Where(x => x.Id == studentId && x.IsActive)
            .Select(x => new FaceUploadViewModel
            {
                StudentId = x.Id,
                StudentCode = x.StudentCode,
                StudentName = x.FullName,
                ClassroomName = x.Classroom != null ? x.Classroom.Name : "-",
                EnrollmentStatus = x.FaceProfile != null ? x.FaceProfile.EnrollmentStatus : EnrollmentStatus.NotRegistered,
                CurrentPhotoCount = x.StudentPhotos.Count,
                ExistingPhotos = x.StudentPhotos
                    .OrderByDescending(p => p.IsPrimary)
                    .ThenByDescending(p => p.CapturedAt)
                    .Select(p => new FaceEnrollmentPhotoViewModel
                    {
                        PhotoId = p.Id,
                        FilePath = p.FilePath,
                        IsPrimary = p.IsPrimary,
                        CapturedAt = p.CapturedAt
                    })
                    .ToList()
            })
            .FirstOrDefaultAsync(cancellationToken);
    }

    private async Task NormalizePrimaryPhotoAsync(int studentId, CancellationToken cancellationToken)
    {
        var photos = await _dbContext.StudentPhotos
            .Where(x => x.StudentId == studentId)
            .OrderByDescending(x => x.IsPrimary)
            .ThenByDescending(x => x.CapturedAt)
            .ToListAsync(cancellationToken);

        if (photos.Count == 0)
        {
            return;
        }

        var preferred = photos.FirstOrDefault(x => x.IsPrimary) ?? photos[0];
        foreach (var photo in photos)
        {
            photo.IsPrimary = photo.Id == preferred.Id;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task<(bool Success, string Message)> RebuildFaceProfileAsync(int studentId, CancellationToken cancellationToken)
    {
        var imagePaths = await _dbContext.StudentPhotos
            .AsNoTracking()
            .Where(x => x.StudentId == studentId)
            .OrderByDescending(x => x.IsPrimary)
            .ThenByDescending(x => x.CapturedAt)
            .Select(x => x.FilePath)
            .ToListAsync(cancellationToken);

        if (imagePaths.Count == 0)
        {
            await _faceRecognitionService.RemoveStudentProfileAsync(studentId);
            return (true, "ไม่มีรูปคงเหลือ ระบบได้ลบโปรไฟล์ใบหน้าออกแล้ว");
        }

        var enrollResult = await _faceRecognitionService.EnrollStudentAsync(studentId, imagePaths);
        return (enrollResult.Success, enrollResult.Message);
    }

    private Task LogAuditAsync(string action, int studentId, string detail, CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var ip = HttpContextHelper.GetIpAddress(HttpContext);
        return _auditLogService.LogAsync(userId, action, "Student", studentId.ToString(), detail, ip, cancellationToken);
    }

    private static List<(IFormFile FormFile, MemoryStream Stream)> ConvertCapturedImagesToFiles(IReadOnlyList<string> capturedImages)
    {
        var files = new List<(IFormFile FormFile, MemoryStream Stream)>();

        var index = 0;
        foreach (var encoded in capturedImages.Where(x => !string.IsNullOrWhiteSpace(x)))
        {
            if (!TryDecodeCapturedImage(encoded, out var bytes, out var contentType, out var extension))
            {
                continue;
            }

            var stream = new MemoryStream(bytes);
            var fileName = $"camera_{DateTime.UtcNow:yyyyMMddHHmmss}_{index}{extension}";
            var formFile = new FormFile(stream, 0, stream.Length, $"CapturedImage{index}", fileName)
            {
                Headers = new HeaderDictionary(),
                ContentType = contentType
            };

            files.Add((formFile, stream));
            index++;
        }

        return files;
    }

    private static void DisposeCapturedStreams(IEnumerable<(IFormFile FormFile, MemoryStream Stream)> capturedFiles)
    {
        foreach (var (_, stream) in capturedFiles)
        {
            stream.Dispose();
        }
    }

    private static bool TryDecodeCapturedImage(
        string encodedValue,
        out byte[] bytes,
        out string contentType,
        out string extension)
    {
        bytes = [];
        contentType = "image/jpeg";
        extension = ".jpg";

        if (string.IsNullOrWhiteSpace(encodedValue))
        {
            return false;
        }

        var payload = encodedValue.Trim();
        if (payload.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
        {
            var commaIndex = payload.IndexOf(',');
            if (commaIndex <= 0)
            {
                return false;
            }

            var metadata = payload.Substring(5, commaIndex - 5);
            var separatorIndex = metadata.IndexOf(';');
            if (separatorIndex < 0 || !metadata.EndsWith(";base64", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            contentType = metadata[..separatorIndex];
            if (!AllowedCapturedContentTypes.Contains(contentType))
            {
                return false;
            }

            extension = contentType switch
            {
                "image/png" => ".png",
                "image/webp" => ".webp",
                _ => ".jpg"
            };

            payload = payload[(commaIndex + 1)..];
        }

        try
        {
            bytes = Convert.FromBase64String(payload);
            return bytes.Length > 0;
        }
        catch
        {
            return false;
        }
    }
}
