using FaceScan.Web.Models.Enums;
using Microsoft.AspNetCore.Http;

namespace FaceScan.Web.ViewModels.StudentPortal;

public class StudentFaceEnrollmentViewModel
{
    public int StudentId { get; set; }
    public string StudentCode { get; set; } = string.Empty;
    public string StudentName { get; set; } = string.Empty;
    public string Classroom { get; set; } = string.Empty;
    public EnrollmentStatus EnrollmentStatus { get; set; }
    public int CurrentPhotoCount { get; set; }
    public int RequiredPhotoCount { get; set; } = 3;
    public IReadOnlyList<string> PhotoPaths { get; set; } = [];
    public IReadOnlyList<StudentFaceEnrollmentPhotoViewModel> Photos { get; set; } = [];
    public List<IFormFile> Files { get; set; } = [];
    public List<string> CapturedImages { get; set; } = [];
}

public class StudentFaceEnrollmentPhotoViewModel
{
    public int PhotoId { get; set; }
    public string FilePath { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long? FileSizeBytes { get; set; }
    public bool IsPrimary { get; set; }
    public DateTime CapturedAt { get; set; }
}
