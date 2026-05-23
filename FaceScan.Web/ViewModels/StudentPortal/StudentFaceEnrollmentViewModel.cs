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
    public List<IFormFile> Files { get; set; } = [];
    public List<string> CapturedImages { get; set; } = [];
}
