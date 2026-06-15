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

[Authorize(Roles = "SuperAdmin,Admin,Teacher,HomeroomHead")]
public class TeacherFaceEnrollmentController : Controller
{
    private static readonly HashSet<string> AllowedCapturedContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg",
        "image/png",
        "image/webp"
    };

    private readonly ApplicationDbContext _dbContext;
    private readonly IWebHostEnvironment _environment;
    private readonly IFileStorageService _fileStorageService;
    private readonly IFaceRecognitionService _faceRecognitionService;
    private readonly IAuditLogService _auditLogService;

    public TeacherFaceEnrollmentController(
        ApplicationDbContext dbContext,
        IWebHostEnvironment environment,
        IFileStorageService fileStorageService,
        IFaceRecognitionService faceRecognitionService,
        IAuditLogService auditLogService)
    {
        _dbContext = dbContext;
        _environment = environment;
        _fileStorageService = fileStorageService;
        _faceRecognitionService = faceRecognitionService;
        _auditLogService = auditLogService;
    }

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var roleNames = TeacherRoleCatalog.AttendanceRoles;

        var teacherQuery = (
            from user in _dbContext.Users.AsNoTracking()
            join userRole in _dbContext.UserRoles.AsNoTracking() on user.Id equals userRole.UserId
            join role in _dbContext.Roles.AsNoTracking() on userRole.RoleId equals role.Id
            join classroom in _dbContext.Classrooms.AsNoTracking() on user.AssignedClassroomId equals classroom.Id into classroomJoin
            from classroom in classroomJoin.DefaultIfEmpty()
            join profile in _dbContext.TeacherFaceProfiles.AsNoTracking() on user.Id equals profile.UserId into profileJoin
            from profile in profileJoin.DefaultIfEmpty()
            where user.IsActive &&
                  role.Name != null &&
                  roleNames.Contains(role.Name)
            select new
            {
                user.Id,
                Username = user.UserName ?? string.Empty,
                user.FullName,
                RoleName = role.Name!,
                AssignedClassroomName = classroom != null ? classroom.Name : "-",
                EnrollmentStatus = profile != null ? profile.EnrollmentStatus : EnrollmentStatus.NotRegistered
            });

        if (!CanManageAllTeachers())
        {
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(currentUserId))
            {
                return Forbid();
            }

            teacherQuery = teacherQuery.Where(x => x.Id == currentUserId);
        }

        var teachers = await teacherQuery.ToListAsync(cancellationToken);

        var photoCounts = await _dbContext.TeacherFacePhotos
            .AsNoTracking()
            .GroupBy(x => x.UserId)
            .Select(x => new { x.Key, Count = x.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count, cancellationToken);

        var items = teachers
            .GroupBy(x => x.Id)
            .Select(x =>
            {
                var teacher = x.First();
                photoCounts.TryGetValue(teacher.Id, out var photoCount);

                return new TeacherFaceEnrollmentTeacherViewModel
                {
                    TeacherId = teacher.Id,
                    Username = teacher.Username,
                    FullName = teacher.FullName,
                    RoleName = teacher.RoleName,
                    AssignedClassroomName = teacher.AssignedClassroomName,
                    EnrollmentStatus = teacher.EnrollmentStatus,
                    PhotoCount = photoCount
                };
            })
            .OrderBy(x => x.FullName)
            .ToList();

        return View(new TeacherFaceEnrollmentIndexViewModel { Teachers = items });
    }

    [HttpGet]
    public async Task<IActionResult> Upload(string teacherId, CancellationToken cancellationToken)
    {
        if (!CanManageTeacher(teacherId))
        {
            return Forbid();
        }

        var model = await BuildUploadViewModelAsync(teacherId, cancellationToken);
        if (model is null)
        {
            return NotFound();
        }

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Upload(TeacherFaceUploadViewModel model, CancellationToken cancellationToken)
    {
        if (!CanManageTeacher(model.TeacherId))
        {
            return Forbid();
        }

        var roleName = await LoadTeacherRoleNameAsync(model.TeacherId, cancellationToken);
        var teacher = await _dbContext.Users
            .Include(x => x.TeacherFacePhotos)
            .FirstOrDefaultAsync(x => x.Id == model.TeacherId && x.IsActive, cancellationToken);

        if (teacher is null || string.IsNullOrWhiteSpace(roleName))
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
            return RedirectToAction(nameof(Upload), new { teacherId = model.TeacherId });
        }

        var hasPrimary = teacher.TeacherFacePhotos.Any(x => x.IsPrimary);
        var addedCount = 0;
        var skippedCount = 0;

        try
        {
            foreach (var file in incomingFiles)
            {
                try
                {
                    var path = await _fileStorageService.SaveTeacherPhotoAsync(file, model.TeacherId, cancellationToken);
                    _dbContext.TeacherFacePhotos.Add(new TeacherFacePhoto
                    {
                        UserId = model.TeacherId,
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
                return RedirectToAction(nameof(Upload), new { teacherId = model.TeacherId });
            }

            await _dbContext.SaveChangesAsync(cancellationToken);

            var rebuildResult = await RebuildFaceProfileAsync(model.TeacherId, cancellationToken);
            var message = rebuildResult.Message;
            if (skippedCount > 0)
            {
                message = $"{message} (ข้ามไฟล์ที่ไม่ถูกต้อง: {skippedCount})";
            }

            await LogAuditAsync("EnrollTeacherFace", model.TeacherId, message, cancellationToken);
            TempData[rebuildResult.Success ? "Success" : "Error"] = message;
            return RedirectToAction(nameof(Upload), new { teacherId = model.TeacherId });
        }
        finally
        {
            DisposeCapturedStreams(capturedFiles);
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetPrimaryPhoto(string teacherId, int photoId, CancellationToken cancellationToken)
    {
        if (!CanManageTeacher(teacherId))
        {
            return Forbid();
        }

        var photos = await _dbContext.TeacherFacePhotos
            .Where(x => x.UserId == teacherId)
            .OrderByDescending(x => x.IsPrimary)
            .ThenByDescending(x => x.CapturedAt)
            .ToListAsync(cancellationToken);

        if (photos.Count == 0)
        {
            TempData["Error"] = "ไม่พบรูปใบหน้าของครูคนนี้";
            return RedirectToAction(nameof(Upload), new { teacherId });
        }

        var target = photos.FirstOrDefault(x => x.Id == photoId);
        if (target is null)
        {
            TempData["Error"] = "ไม่พบรูปที่ต้องการ";
            return RedirectToAction(nameof(Upload), new { teacherId });
        }

        foreach (var photo in photos)
        {
            photo.IsPrimary = photo.Id == photoId;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        await LogAuditAsync("SetTeacherPrimaryFacePhoto", teacherId, $"ตั้งรูปหลักเป็นรหัสรูป {photoId}", cancellationToken);

        TempData["Success"] = "ตั้งค่ารูปหลักเรียบร้อย";
        return RedirectToAction(nameof(Upload), new { teacherId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeletePhoto(string teacherId, int photoId, CancellationToken cancellationToken)
    {
        if (!CanManageTeacher(teacherId))
        {
            return Forbid();
        }

        var photo = await _dbContext.TeacherFacePhotos
            .FirstOrDefaultAsync(x => x.Id == photoId && x.UserId == teacherId, cancellationToken);

        if (photo is null)
        {
            TempData["Error"] = "ไม่พบรูปที่ต้องการ";
            return RedirectToAction(nameof(Upload), new { teacherId });
        }

        var filePath = photo.FilePath;
        _dbContext.TeacherFacePhotos.Remove(photo);
        await _dbContext.SaveChangesAsync(cancellationToken);
        await _fileStorageService.DeleteFileIfExistsAsync(filePath);

        await NormalizePrimaryPhotoAsync(teacherId, cancellationToken);
        var rebuildResult = await RebuildFaceProfileAsync(teacherId, cancellationToken);

        var message = $"ลบรูปใบหน้าเรียบร้อย {rebuildResult.Message}";
        await LogAuditAsync("DeleteTeacherFacePhoto", teacherId, message, cancellationToken);
        TempData[rebuildResult.Success ? "Success" : "Error"] = message;
        return RedirectToAction(nameof(Upload), new { teacherId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ClearEnrollment(string teacherId, CancellationToken cancellationToken)
    {
        if (!CanManageTeacher(teacherId))
        {
            return Forbid();
        }

        var roleName = await LoadTeacherRoleNameAsync(teacherId, cancellationToken);
        var teacher = await _dbContext.Users
            .Include(x => x.TeacherFacePhotos)
            .FirstOrDefaultAsync(x => x.Id == teacherId && x.IsActive, cancellationToken);

        if (teacher is null || string.IsNullOrWhiteSpace(roleName))
        {
            return NotFound();
        }

        var photoPaths = teacher.TeacherFacePhotos.Select(x => x.FilePath).ToList();
        if (teacher.TeacherFacePhotos.Count > 0)
        {
            _dbContext.TeacherFacePhotos.RemoveRange(teacher.TeacherFacePhotos);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        foreach (var path in photoPaths)
        {
            await _fileStorageService.DeleteFileIfExistsAsync(path);
        }

        await _faceRecognitionService.RemoveTeacherProfileAsync(teacherId);
        await LogAuditAsync("ClearTeacherFaceEnrollment", teacherId, "ล้างข้อมูลลงทะเบียนใบหน้าเรียบร้อย", cancellationToken);

        TempData["Success"] = "รีเซ็ตการลงทะเบียนใบหน้าเรียบร้อย";
        return RedirectToAction(nameof(Upload), new { teacherId });
    }

    private async Task<TeacherFaceUploadViewModel?> BuildUploadViewModelAsync(string teacherId, CancellationToken cancellationToken)
    {
        var roleName = await LoadTeacherRoleNameAsync(teacherId, cancellationToken);
        if (string.IsNullOrWhiteSpace(roleName))
        {
            return null;
        }

        var teacher = await _dbContext.Users
            .AsNoTracking()
            .Include(x => x.AssignedClassroom)
            .Include(x => x.TeacherFaceProfile)
            .Include(x => x.TeacherFacePhotos)
            .FirstOrDefaultAsync(x => x.Id == teacherId && x.IsActive, cancellationToken);

        if (teacher is null)
        {
            return null;
        }

        return new TeacherFaceUploadViewModel
        {
            TeacherId = teacher.Id,
            Username = teacher.UserName ?? string.Empty,
            TeacherName = teacher.FullName,
            RoleName = roleName,
            AssignedClassroomName = teacher.AssignedClassroom?.Name ?? "-",
            EnrollmentStatus = teacher.TeacherFaceProfile?.EnrollmentStatus ?? EnrollmentStatus.NotRegistered,
            CurrentPhotoCount = teacher.TeacherFacePhotos.Count,
            ExistingPhotos = teacher.TeacherFacePhotos
                .OrderByDescending(x => x.IsPrimary)
                .ThenByDescending(x => x.CapturedAt)
                .Select(x => new TeacherFaceEnrollmentPhotoViewModel
                {
                    PhotoId = x.Id,
                    FilePath = x.FilePath,
                    ContentType = x.ContentType,
                    FileSizeBytes = ResolvePhotoFileSize(x.FilePath),
                    IsPrimary = x.IsPrimary,
                    CapturedAt = x.CapturedAt
                })
                .ToList()
        };
    }

    private long? ResolvePhotoFileSize(string filePath)
    {
        if (string.IsNullOrWhiteSpace(_environment.WebRootPath) || string.IsNullOrWhiteSpace(filePath))
        {
            return null;
        }

        var relativePath = filePath.TrimStart('/', '\\').Replace('/', Path.DirectorySeparatorChar);
        var fullPath = Path.Combine(_environment.WebRootPath, relativePath);
        return System.IO.File.Exists(fullPath) ? new FileInfo(fullPath).Length : null;
    }

    private async Task<string?> LoadTeacherRoleNameAsync(string teacherId, CancellationToken cancellationToken)
    {
        return await (
            from userRole in _dbContext.UserRoles.AsNoTracking()
            join role in _dbContext.Roles.AsNoTracking() on userRole.RoleId equals role.Id
            where userRole.UserId == teacherId &&
                  role.Name != null &&
                  TeacherRoleCatalog.AttendanceRoles.Contains(role.Name)
            select role.Name)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private async Task NormalizePrimaryPhotoAsync(string teacherId, CancellationToken cancellationToken)
    {
        var photos = await _dbContext.TeacherFacePhotos
            .Where(x => x.UserId == teacherId)
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

    private async Task<(bool Success, string Message)> RebuildFaceProfileAsync(string teacherId, CancellationToken cancellationToken)
    {
        var imagePaths = await _dbContext.TeacherFacePhotos
            .AsNoTracking()
            .Where(x => x.UserId == teacherId)
            .OrderByDescending(x => x.IsPrimary)
            .ThenByDescending(x => x.CapturedAt)
            .Select(x => x.FilePath)
            .ToListAsync(cancellationToken);

        if (imagePaths.Count == 0)
        {
            await _faceRecognitionService.RemoveTeacherProfileAsync(teacherId);
            return (true, "ไม่มีรูปคงเหลือ ระบบได้ลบโปรไฟล์ใบหน้าออกแล้ว");
        }

        var enrollResult = await _faceRecognitionService.EnrollTeacherAsync(teacherId, imagePaths);
        return (enrollResult.Success, enrollResult.Message);
    }

    private Task LogAuditAsync(string action, string teacherId, string detail, CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var ip = HttpContextHelper.GetIpAddress(HttpContext);
        return _auditLogService.LogAsync(userId, action, "Teacher", teacherId, detail, ip, cancellationToken);
    }

    private bool CanManageAllTeachers()
    {
        return User.IsInRole("SuperAdmin") || User.IsInRole("Admin");
    }

    private bool CanManageTeacher(string teacherId)
    {
        if (string.IsNullOrWhiteSpace(teacherId))
        {
            return false;
        }

        if (CanManageAllTeachers())
        {
            return true;
        }

        var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return !string.IsNullOrWhiteSpace(currentUserId) &&
               string.Equals(currentUserId, teacherId, StringComparison.Ordinal);
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

            extension = contentType.ToLowerInvariant() switch
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
        catch (FormatException)
        {
            return false;
        }
    }
}
