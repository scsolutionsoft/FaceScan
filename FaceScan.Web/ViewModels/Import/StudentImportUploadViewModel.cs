using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace FaceScan.Web.ViewModels.Import;

public class StudentImportUploadViewModel
{
    [Required]
    public ImportDataType ImportType { get; set; } = ImportDataType.Students;

    [Required]
    public IFormFile? File { get; set; }
}
