using FaceScan.Web.Helpers;
using FaceScan.Web.Models.Entities;
using FaceScan.Web.ViewModels.Students;

namespace FaceScan.Web.Repositories.Interfaces;

public interface IStudentRepository : IRepository<Student>
{
    Task<Student?> GetDetailAsync(int id, CancellationToken cancellationToken = default);
    Task<PagedResult<Student>> SearchAsync(StudentFilterViewModel filter, CancellationToken cancellationToken = default);
    Task<Student?> FindByCodeAsync(string studentCode, CancellationToken cancellationToken = default);
}
