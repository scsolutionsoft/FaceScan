using FaceScan.Web.Data;
using FaceScan.Web.Helpers;
using FaceScan.Web.Models.Entities;
using FaceScan.Web.Repositories.Interfaces;
using FaceScan.Web.ViewModels.Students;
using Microsoft.EntityFrameworkCore;

namespace FaceScan.Web.Repositories;

public class StudentRepository : Repository<Student>, IStudentRepository
{
    public StudentRepository(ApplicationDbContext dbContext)
        : base(dbContext)
    {
    }

    public Task<Student?> GetDetailAsync(int id, CancellationToken cancellationToken = default)
    {
        return DbContext.Students
            .Include(x => x.AcademicYear)
            .Include(x => x.GradeLevel)
            .Include(x => x.Classroom)
            .Include(x => x.FaceProfile)
            .Include(x => x.StudentPhotos)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    public async Task<PagedResult<Student>> SearchAsync(StudentFilterViewModel filter, CancellationToken cancellationToken = default)
    {
        var query = DbContext.Students
            .Include(x => x.AcademicYear)
            .Include(x => x.GradeLevel)
            .Include(x => x.Classroom)
            .Include(x => x.FaceProfile)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(filter.SearchText))
        {
            var keyword = filter.SearchText.Trim();
            query = query.Where(x =>
                x.StudentCode.Contains(keyword) ||
                x.FirstName.Contains(keyword) ||
                x.LastName.Contains(keyword) ||
                (x.NationalId != null && x.NationalId.Contains(keyword)) ||
                (x.GuardianNationalId != null && x.GuardianNationalId.Contains(keyword)));
        }

        if (filter.AcademicYearId.HasValue)
        {
            query = query.Where(x => x.AcademicYearId == filter.AcademicYearId.Value);
        }

        if (filter.GradeLevelId.HasValue)
        {
            query = query.Where(x => x.GradeLevelId == filter.GradeLevelId.Value);
        }

        if (filter.ClassroomId.HasValue)
        {
            query = query.Where(x => x.ClassroomId == filter.ClassroomId.Value);
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var skip = (Math.Max(filter.Page, 1) - 1) * Math.Max(filter.PageSize, 1);

        var items = await query
            .OrderBy(x => x.StudentCode)
            .Skip(skip)
            .Take(filter.PageSize)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return new PagedResult<Student>
        {
            Items = items,
            TotalCount = totalCount,
            Page = filter.Page,
            PageSize = filter.PageSize
        };
    }

    public Task<Student?> FindByCodeAsync(string studentCode, CancellationToken cancellationToken = default)
    {
        return DbContext.Students
            .Include(x => x.FaceProfile)
            .Include(x => x.Classroom)
            .Include(x => x.GradeLevel)
            .Include(x => x.AcademicYear)
            .FirstOrDefaultAsync(x => x.StudentCode == studentCode, cancellationToken);
    }
}
