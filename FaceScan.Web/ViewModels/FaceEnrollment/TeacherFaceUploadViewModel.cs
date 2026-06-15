using System.ComponentModel.DataAnnotations;
using FaceScan.Web.Models.Enums;
using Microsoft.AspNetCore.Http;

namespace FaceScan.Web.ViewModels.FaceEnrollment;

public class TeacherFaceUploadViewModel
{
    [Required]
    public string TeacherId { get; set; } = string.Empty;

    public List<IFormFile> Files { get; set; } = [];

    public List<string> CapturedImages { get; set; } = [];

    public string Username { get; set; } = string.Empty;
    public string TeacherName { get; set; } = string.Empty;
    public string RoleName { get; set; } = string.Empty;
    public string AssignedClassroomName { get; set; } = string.Empty;
    public EnrollmentStatus EnrollmentStatus { get; set; }
    public int CurrentPhotoCount { get; set; }
    public IReadOnlyList<TeacherFaceEnrollmentPhotoViewModel> ExistingPhotos { get; set; } = [];
}

public class TeacherFaceEnrollmentPhotoViewModel
{
    public int PhotoId { get; set; }
    public string FilePath { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long? FileSizeBytes { get; set; }
    public bool IsPrimary { get; set; }
    public DateTime CapturedAt { get; set; }
}
