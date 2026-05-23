using FaceScan.Web.ViewModels.Import;
using Microsoft.AspNetCore.Http;

namespace FaceScan.Web.Services.Interfaces;

public interface IStudentImportService
{
    Task<StudentImportIndexViewModel> PreviewAsync(IFormFile file, CancellationToken cancellationToken = default);
    Task<StudentImportResultViewModel> ImportAsync(string previewToken, string? importedByUserId, string? ipAddress, CancellationToken cancellationToken = default);
    byte[] GenerateTemplateCsv();
}
