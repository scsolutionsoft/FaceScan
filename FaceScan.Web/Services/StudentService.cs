using FaceScan.Web.Data;
using FaceScan.Web.Helpers;
using FaceScan.Web.Mappings;
using FaceScan.Web.Models.Entities;
using FaceScan.Web.Models.Enums;
using FaceScan.Web.Repositories.Interfaces;
using FaceScan.Web.Services.Interfaces;
using FaceScan.Web.Validators;
using FaceScan.Web.ViewModels;
using FaceScan.Web.ViewModels.Students;
using Microsoft.EntityFrameworkCore;

namespace FaceScan.Web.Services;

public class StudentService : IStudentService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly IStudentRepository _studentRepository;
    private readonly IAuditLogService _auditLogService;

    public StudentService(
        ApplicationDbContext dbContext,
        IStudentRepository studentRepository,
        IAuditLogService auditLogService)
    {
        _dbContext = dbContext;
        _studentRepository = studentRepository;
        _auditLogService = auditLogService;
    }

    public async Task<PagedResult<StudentListItemViewModel>> GetStudentsAsync(StudentFilterViewModel filter, CancellationToken cancellationToken = default)
    {
        var result = await _studentRepository.SearchAsync(filter, cancellationToken);
        return new PagedResult<StudentListItemViewModel>
        {
            Items = result.Items.Select(x => x.ToListItemViewModel()).ToList(),
            TotalCount = result.TotalCount,
            Page = result.Page,
            PageSize = result.PageSize
        };
    }

    public async Task<StudentDetailViewModel?> GetStudentDetailAsync(int id, CancellationToken cancellationToken = default)
    {
        var student = await _studentRepository.GetDetailAsync(id, cancellationToken);
        return student?.ToDetailViewModel();
    }

    public async Task<StudentUpsertViewModel?> GetForEditAsync(int id, CancellationToken cancellationToken = default)
    {
        var student = await _studentRepository.GetByIdAsync(id, cancellationToken);
        if (student is null)
        {
            return null;
        }

        return new StudentUpsertViewModel
        {
            Id = student.Id,
            StudentCode = student.StudentCode,
            NationalId = student.NationalId,
            Prefix = student.Prefix,
            FirstName = student.FirstName,
            LastName = student.LastName,
            FirstNameEn = student.FirstNameEn,
            LastNameEn = student.LastNameEn,
            Gender = student.Gender,
            BirthDate = student.BirthDate,
            AcademicYearId = student.AcademicYearId,
            GradeLevelId = student.GradeLevelId,
            ClassroomId = student.ClassroomId,
            RoomNumber = student.RoomNumber,
            StudentNo = student.StudentNo,
            Status = student.Status,
            GuardianName = student.GuardianName,
            GuardianPhone = student.GuardianPhone,
            Address = student.Address,
            Notes = student.Notes,
            IsActive = student.IsActive
        };
    }

    public async Task<(bool Success, string Message, int StudentId)> CreateAsync(
        StudentUpsertViewModel model,
        string? userId,
        string? ipAddress,
        CancellationToken cancellationToken = default)
    {
        var validationErrors = StudentValidator.Validate(model);
        if (validationErrors.Count > 0)
        {
            return (false, string.Join("; ", validationErrors), 0);
        }

        var duplicated = await _dbContext.Students.AnyAsync(x => x.StudentCode == model.StudentCode, cancellationToken);
        if (duplicated)
        {
            return (false, "รหัสนักเรียนนี้ถูกใช้งานแล้ว", 0);
        }

        var student = new Student
        {
            StudentCode = model.StudentCode.Trim(),
            NationalId = model.NationalId?.Trim(),
            Prefix = model.Prefix.Trim(),
            FirstName = model.FirstName.Trim(),
            LastName = model.LastName.Trim(),
            FirstNameEn = model.FirstNameEn?.Trim(),
            LastNameEn = model.LastNameEn?.Trim(),
            Gender = model.Gender,
            BirthDate = model.BirthDate,
            AcademicYearId = model.AcademicYearId,
            GradeLevelId = model.GradeLevelId,
            ClassroomId = model.ClassroomId,
            RoomNumber = model.RoomNumber?.Trim(),
            StudentNo = model.StudentNo?.Trim(),
            Status = model.Status,
            GuardianName = model.GuardianName?.Trim(),
            GuardianPhone = model.GuardianPhone?.Trim(),
            Address = model.Address?.Trim(),
            Notes = model.Notes?.Trim(),
            IsActive = model.IsActive
        };

        _dbContext.Students.Add(student);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _dbContext.FaceProfiles.Add(new FaceProfile
        {
            StudentId = student.Id,
            EnrollmentStatus = EnrollmentStatus.NotRegistered,
            TemplateVersion = "v1"
        });
        await _dbContext.SaveChangesAsync(cancellationToken);

        await _auditLogService.LogAsync(
            userId,
            "Create",
            "Student",
            student.Id.ToString(),
            $"สร้างข้อมูลนักเรียน {student.StudentCode}",
            ipAddress,
            cancellationToken);

        return (true, "สร้างข้อมูลนักเรียนเรียบร้อย", student.Id);
    }

    public async Task<(bool Success, string Message)> UpdateAsync(
        StudentUpsertViewModel model,
        string? userId,
        string? ipAddress,
        CancellationToken cancellationToken = default)
    {
        if (!model.Id.HasValue)
        {
            return (false, "ไม่พบรหัสนักเรียน");
        }

        var validationErrors = StudentValidator.Validate(model);
        if (validationErrors.Count > 0)
        {
            return (false, string.Join("; ", validationErrors));
        }

        var student = await _dbContext.Students.FirstOrDefaultAsync(x => x.Id == model.Id.Value, cancellationToken);
        if (student is null)
        {
            return (false, "ไม่พบนักเรียน");
        }

        var duplicated = await _dbContext.Students.AnyAsync(
            x => x.StudentCode == model.StudentCode && x.Id != model.Id.Value,
            cancellationToken);
        if (duplicated)
        {
            return (false, "รหัสนักเรียนนี้ถูกใช้งานแล้ว");
        }

        student.StudentCode = model.StudentCode.Trim();
        student.NationalId = model.NationalId?.Trim();
        student.Prefix = model.Prefix.Trim();
        student.FirstName = model.FirstName.Trim();
        student.LastName = model.LastName.Trim();
        student.FirstNameEn = model.FirstNameEn?.Trim();
        student.LastNameEn = model.LastNameEn?.Trim();
        student.Gender = model.Gender;
        student.BirthDate = model.BirthDate;
        student.AcademicYearId = model.AcademicYearId;
        student.GradeLevelId = model.GradeLevelId;
        student.ClassroomId = model.ClassroomId;
        student.RoomNumber = model.RoomNumber?.Trim();
        student.StudentNo = model.StudentNo?.Trim();
        student.Status = model.Status;
        student.GuardianName = model.GuardianName?.Trim();
        student.GuardianPhone = model.GuardianPhone?.Trim();
        student.Address = model.Address?.Trim();
        student.Notes = model.Notes?.Trim();
        student.IsActive = model.IsActive;

        await _dbContext.SaveChangesAsync(cancellationToken);

        await _auditLogService.LogAsync(
            userId,
            "Update",
            "Student",
            student.Id.ToString(),
            $"แก้ไขข้อมูลนักเรียน {student.StudentCode}",
            ipAddress,
            cancellationToken);

        return (true, "บันทึกข้อมูลเรียบร้อย");
    }

    public async Task<(bool Success, string Message)> DeactivateAsync(
        int id,
        string? userId,
        string? ipAddress,
        CancellationToken cancellationToken = default)
    {
        var student = await _dbContext.Students.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (student is null)
        {
            return (false, "ไม่พบนักเรียน");
        }

        student.IsActive = false;
        student.Status = StudentStatus.Inactive;
        await _dbContext.SaveChangesAsync(cancellationToken);

        await _auditLogService.LogAsync(
            userId,
            "Deactivate",
            "Student",
            student.Id.ToString(),
            $"ปิดการใช้งานนักเรียน {student.StudentCode}",
            ipAddress,
            cancellationToken);

        return (true, "ปิดการใช้งานนักเรียนเรียบร้อย");
    }

    public async Task<IReadOnlyList<SelectOptionViewModel>> GetAcademicYearOptionsAsync(CancellationToken cancellationToken = default)
    {
        return await _dbContext.AcademicYears
            .AsNoTracking()
            .Where(x => x.IsActive)
            .OrderByDescending(x => x.IsCurrent)
            .ThenByDescending(x => x.StartDate)
            .Select(x => new SelectOptionViewModel
            {
                Value = x.Id,
                Text = x.Name
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<SelectOptionViewModel>> GetGradeLevelOptionsAsync(CancellationToken cancellationToken = default)
    {
        return await _dbContext.GradeLevels
            .AsNoTracking()
            .Where(x => x.IsActive)
            .OrderBy(x => x.SortOrder)
            .Select(x => new SelectOptionViewModel
            {
                Value = x.Id,
                Text = x.Name
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<SelectOptionViewModel>> GetClassroomOptionsAsync(CancellationToken cancellationToken = default)
    {
        return await _dbContext.Classrooms
            .AsNoTracking()
            .Where(x => x.IsActive)
            .OrderBy(x => x.Name)
            .Select(x => new SelectOptionViewModel
            {
                Value = x.Id,
                Text = x.Name
            })
            .ToListAsync(cancellationToken);
    }

    public Task<Student?> FindByCodeAsync(string studentCode, CancellationToken cancellationToken = default)
    {
        return _studentRepository.FindByCodeAsync(studentCode, cancellationToken);
    }
}
