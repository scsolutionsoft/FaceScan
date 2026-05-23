using FaceScan.Web.Helpers;
using FaceScan.Web.Models.Entities;
using FaceScan.Web.ViewModels;
using FaceScan.Web.ViewModels.Students;

namespace FaceScan.Web.Services.Interfaces;

public interface IStudentService
{
    Task<PagedResult<StudentListItemViewModel>> GetStudentsAsync(StudentFilterViewModel filter, CancellationToken cancellationToken = default);
    Task<StudentDetailViewModel?> GetStudentDetailAsync(int id, CancellationToken cancellationToken = default);
    Task<StudentUpsertViewModel?> GetForEditAsync(int id, CancellationToken cancellationToken = default);
    Task<(bool Success, string Message, int StudentId)> CreateAsync(StudentUpsertViewModel model, string? userId, string? ipAddress, CancellationToken cancellationToken = default);
    Task<(bool Success, string Message)> UpdateAsync(StudentUpsertViewModel model, string? userId, string? ipAddress, CancellationToken cancellationToken = default);
    Task<(bool Success, string Message)> DeactivateAsync(int id, string? userId, string? ipAddress, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SelectOptionViewModel>> GetAcademicYearOptionsAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SelectOptionViewModel>> GetGradeLevelOptionsAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SelectOptionViewModel>> GetClassroomOptionsAsync(CancellationToken cancellationToken = default);
    Task<Student?> FindByCodeAsync(string studentCode, CancellationToken cancellationToken = default);
}
