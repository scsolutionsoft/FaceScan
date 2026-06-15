using FaceScan.Web.Helpers;
using FaceScan.Web.Models.Options;
using FaceScan.Web.Services.Interfaces;
using FaceScan.Web.Validators;
using Microsoft.Extensions.Options;

namespace FaceScan.Web.Services;

public class FileStorageService : IFileStorageService
{
    private readonly IWebHostEnvironment _environment;
    private readonly UploadSettings _uploadSettings;

    public FileStorageService(IWebHostEnvironment environment, IOptions<UploadSettings> uploadSettings)
    {
        _environment = environment;
        _uploadSettings = uploadSettings.Value;
    }

    public async Task<string> SaveStudentPhotoAsync(IFormFile file, int studentId, CancellationToken cancellationToken = default)
    {
        if (!FileValidator.IsValidImage(file, _uploadSettings))
        {
            throw new InvalidOperationException("ไฟล์อูปไม่ถูกต้องหอือขนาดเกินกำหนด");
        }

        var fileName = FileNameHelper.CreateStoredFileName(file.FileName);
        var folderPath = FileNameHelper.BuildSafePath(_environment.WebRootPath, "uploads", "students", studentId.ToString());
        Directory.CreateDirectory(folderPath);
        var fullPath = FileNameHelper.BuildSafePath(folderPath, fileName);

        await using var stream = File.Create(fullPath);
        await file.CopyToAsync(stream, cancellationToken);

        return $"uploads/students/{studentId}/{fileName}".Replace("\\", "/");
    }

    public async Task<string> SaveStudentCarePhotoAsync(IFormFile file, int studentId, string category, CancellationToken cancellationToken = default)
    {
        if (!FileValidator.IsValidImage(file, _uploadSettings))
        {
            throw new InvalidOperationException("ไฟล์อัปโหลดไม่ถูกต้องหรือขนาดเกินกำหนด");
        }

        var safeCategory = string.IsNullOrWhiteSpace(category)
            ? "general"
            : new string(category.Trim().Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();

        if (string.IsNullOrWhiteSpace(safeCategory))
        {
            safeCategory = "general";
        }

        var fileName = FileNameHelper.CreateStoredFileName(file.FileName);
        var folderPath = FileNameHelper.BuildSafePath(_environment.WebRootPath, "uploads", "student-care", studentId.ToString(), safeCategory);
        Directory.CreateDirectory(folderPath);
        var fullPath = FileNameHelper.BuildSafePath(folderPath, fileName);

        await using var stream = File.Create(fullPath);
        await file.CopyToAsync(stream, cancellationToken);

        return $"uploads/student-care/{studentId}/{safeCategory}/{fileName}".Replace("\\", "/");
    }

    public async Task<string> SaveTeacherPhotoAsync(IFormFile file, string userId, CancellationToken cancellationToken = default)
    {
        if (!FileValidator.IsValidImage(file, _uploadSettings))
        {
            throw new InvalidOperationException("ไฟล์อัปโหลดไม่ถูกต้องหรือขนาดเกินกำหนด");
        }

        var fileName = FileNameHelper.CreateStoredFileName(file.FileName);
        var safeUserFolder = string.IsNullOrWhiteSpace(userId) ? "unknown" : userId.Trim();
        var folderPath = FileNameHelper.BuildSafePath(_environment.WebRootPath, "uploads", "teachers", safeUserFolder);
        Directory.CreateDirectory(folderPath);
        var fullPath = FileNameHelper.BuildSafePath(folderPath, fileName);

        await using var stream = File.Create(fullPath);
        await file.CopyToAsync(stream, cancellationToken);

        return $"uploads/teachers/{safeUserFolder}/{fileName}".Replace("\\", "/");
    }

    public async Task<string> SaveBrandLogoAsync(IFormFile file, CancellationToken cancellationToken = default)
    {
        if (!FileValidator.IsValidImage(file, _uploadSettings))
        {
            throw new InvalidOperationException("ไฟล์โลโก้ไม่ถูกต้องหรือขนาดเกินกำหนด");
        }

        var fileName = FileNameHelper.CreateStoredFileName(file.FileName);
        var folderPath = FileNameHelper.BuildSafePath(_environment.WebRootPath, "uploads", "branding");
        Directory.CreateDirectory(folderPath);
        var fullPath = FileNameHelper.BuildSafePath(folderPath, fileName);

        await using var stream = File.Create(fullPath);
        await file.CopyToAsync(stream, cancellationToken);

        return $"uploads/branding/{fileName}".Replace("\\", "/");
    }

    public async Task<string> SaveScanSnapshotAsync(Stream stream, string extension, CancellationToken cancellationToken = default)
    {
        var safeExt = extension.StartsWith('.') ? extension : ".jpg";
        if (safeExt.Length > 5)
        {
            safeExt = ".jpg";
        }

        var fileName = $"scan_{DateTime.UtcNow:yyyyMMddHHmmss}_{Guid.NewGuid():N}{safeExt}";
        var folderPath = FileNameHelper.BuildSafePath(_environment.WebRootPath, "uploads", "scans");
        Directory.CreateDirectory(folderPath);
        var fullPath = FileNameHelper.BuildSafePath(folderPath, fileName);

        stream.Position = 0;
        await using var targetStream = File.Create(fullPath);
        await stream.CopyToAsync(targetStream, cancellationToken);

        return $"uploads/scans/{fileName}".Replace("\\", "/");
    }

    public async Task<string> SaveImportFileAsync(IFormFile file, CancellationToken cancellationToken = default)
    {
        var extension = Path.GetExtension(file.FileName);
        var allowedExtensions = new[] { ".csv", ".xlsx" };
        if (!allowedExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("รองรับเฉพาะไฟล์ .xlsx หรือ .csv");
        }

        var fileName = FileNameHelper.CreateStoredFileName(file.FileName);
        var folderPath = FileNameHelper.BuildSafePath(_environment.WebRootPath, "uploads", "imports");
        Directory.CreateDirectory(folderPath);
        var fullPath = FileNameHelper.BuildSafePath(folderPath, fileName);

        await using var stream = File.Create(fullPath);
        await file.CopyToAsync(stream, cancellationToken);

        return $"uploads/imports/{fileName}".Replace("\\", "/");
    }

    public Task DeleteFileIfExistsAsync(string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
        {
            return Task.CompletedTask;
        }

        var fullPath = FileNameHelper.BuildSafePath(_environment.WebRootPath, relativePath.Replace('/', Path.DirectorySeparatorChar));
        if (File.Exists(fullPath))
        {
            File.Delete(fullPath);
        }

        return Task.CompletedTask;
    }
}
