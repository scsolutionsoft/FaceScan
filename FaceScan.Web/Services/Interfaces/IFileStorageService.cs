using Microsoft.AspNetCore.Http;

namespace FaceScan.Web.Services.Interfaces;

public interface IFileStorageService
{
    Task<string> SaveStudentPhotoAsync(IFormFile file, int studentId, CancellationToken cancellationToken = default);
    Task<string> SaveTeacherPhotoAsync(IFormFile file, string userId, CancellationToken cancellationToken = default);
    Task<string> SaveBrandLogoAsync(IFormFile file, CancellationToken cancellationToken = default);
    Task<string> SaveScanSnapshotAsync(Stream stream, string extension, CancellationToken cancellationToken = default);
    Task<string> SaveImportFileAsync(IFormFile file, CancellationToken cancellationToken = default);
    Task DeleteFileIfExistsAsync(string relativePath);
}
