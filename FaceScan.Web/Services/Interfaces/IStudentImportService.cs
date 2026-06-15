using FaceScan.Web.ViewModels.Import;
using Microsoft.AspNetCore.Http;

namespace FaceScan.Web.Services.Interfaces;

public interface IStudentImportService
{
    Task<StudentImportIndexViewModel> PreviewAsync(ImportDataType importType, IFormFile file, CancellationToken cancellationToken = default);
    Task<StudentImportResultViewModel> ImportAsync(string previewToken, string? importedByUserId, string? ipAddress, CancellationToken cancellationToken = default);
    byte[] GenerateTemplateWorkbook(ImportDataType importType);
}
