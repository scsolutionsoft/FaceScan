using FaceScan.Web.Models.Entities;
using FaceScan.Web.ViewModels.Students;

namespace FaceScan.Web.Mappings;

public static class StudentMappingExtensions
{
    public static StudentListItemViewModel ToListItemViewModel(this Student student)
    {
        return new StudentListItemViewModel
        {
            Id = student.Id,
            StudentCode = student.StudentCode,
            FullName = student.FullName,
            AcademicYearName = student.AcademicYear?.Name ?? string.Empty,
            GradeLevelName = student.GradeLevel?.Name ?? string.Empty,
            ClassroomName = student.Classroom?.Name ?? string.Empty,
            StudentNo = student.StudentNo,
            IsActive = student.IsActive,
            EnrollmentStatus = student.FaceProfile?.EnrollmentStatus ?? Models.Enums.EnrollmentStatus.NotRegistered
        };
    }

    public static StudentDetailViewModel ToDetailViewModel(this Student student)
    {
        return new StudentDetailViewModel
        {
            Id = student.Id,
            StudentCode = student.StudentCode,
            FullName = student.FullName,
            NationalId = student.NationalId,
            Gender = student.Gender,
            BirthDate = student.BirthDate,
            AcademicYearName = student.AcademicYear?.Name ?? string.Empty,
            GradeLevelName = student.GradeLevel?.Name ?? string.Empty,
            ClassroomName = student.Classroom?.Name ?? string.Empty,
            StudentNo = student.StudentNo,
            GuardianName = student.GuardianName,
            GuardianPhone = student.GuardianPhone,
            Address = student.Address,
            Notes = student.Notes,
            IsActive = student.IsActive,
            EnrollmentStatus = student.FaceProfile?.EnrollmentStatus ?? Models.Enums.EnrollmentStatus.NotRegistered,
            Photos = student.StudentPhotos
                .OrderByDescending(x => x.IsPrimary)
                .ThenByDescending(x => x.CapturedAt)
                .Select(x => new StudentPhotoViewModel
                {
                    Id = x.Id,
                    FilePath = x.FilePath,
                    FileName = x.FileName,
                    IsPrimary = x.IsPrimary,
                    QualityScore = x.QualityScore
                }).ToList()
        };
    }
}
