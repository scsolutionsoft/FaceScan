using FaceScan.Web.Models.Entities;

namespace FaceScan.Web.Services.Interfaces;

public interface IFaceRecognitionService
{
    Task<FaceEnrollResult> EnrollStudentAsync(int studentId, List<string> imagePaths);
    Task<FaceEnrollResult> EnrollTeacherAsync(string userId, List<string> imagePaths);
    Task<FaceMatchResult> VerifyAsync(Stream imageStream, string? recognitionProfile = null);
    Task<FaceMatchResult> VerifyTeacherAsync(Stream imageStream, string? recognitionProfile = null);
    Task<bool> RemoveStudentProfileAsync(int studentId);
    Task<bool> RemoveTeacherProfileAsync(string userId);
}
