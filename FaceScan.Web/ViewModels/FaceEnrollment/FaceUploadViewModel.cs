using FaceScan.Web.Models.Enums;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace FaceScan.Web.ViewModels.FaceEnrollment;

public class FaceUploadViewModel
{
    [Required]
    public int StudentId { get; set; }

    public List<IFormFile> Files { get; set; } = [];

    public List<string> CapturedImages { get; set; } = [];

    public string StudentCode { get; set; } = string.Empty;
    public string StudentName { get; set; } = string.Empty;
    public string ClassroomName { get; set; } = string.Empty;
    public EnrollmentStatus EnrollmentStatus { get; set; }
    public int CurrentPhotoCount { get; set; }
    public IReadOnlyList<FaceEnrollmentPhotoViewModel> ExistingPhotos { get; set; } = [];
}

public class FaceEnrollmentPhotoViewModel
{
    public int PhotoId { get; set; }
    public string FilePath { get; set; } = string.Empty;
    public bool IsPrimary { get; set; }
    public DateTime CapturedAt { get; set; }
}
