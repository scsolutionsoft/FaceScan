using System.Collections.Concurrent;
using System.Globalization;
using ClosedXML.Excel;
using CsvHelper;
using CsvHelper.Configuration;
using FaceScan.Web.Data;
using FaceScan.Web.Models.Entities;
using FaceScan.Web.Models.Enums;
using FaceScan.Web.Services.Interfaces;
using FaceScan.Web.ViewModels.Import;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace FaceScan.Web.Services;

public class StudentImportService : IStudentImportService
{
    private static readonly ConcurrentDictionary<string, ImportPreviewContext> PreviewStore = new();

    private readonly ApplicationDbContext _dbContext;
    private readonly IFileStorageService _fileStorageService;
    private readonly IAuditLogService _auditLogService;
    private readonly IWebHostEnvironment _environment;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<IdentityRole> _roleManager;

    public StudentImportService(
        ApplicationDbContext dbContext,
        IFileStorageService fileStorageService,
        IAuditLogService auditLogService,
        IWebHostEnvironment environment,
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole> roleManager)
    {
        _dbContext = dbContext;
        _fileStorageService = fileStorageService;
        _auditLogService = auditLogService;
        _environment = environment;
        _userManager = userManager;
        _roleManager = roleManager;
    }

    public async Task<StudentImportIndexViewModel> PreviewAsync(
        ImportDataType importType,
        IFormFile file,
        CancellationToken cancellationToken = default)
    {
        var storedPath = await _fileStorageService.SaveImportFileAsync(file, cancellationToken);
        var absolutePath = Path.Combine(_environment.WebRootPath, storedPath.Replace('/', Path.DirectorySeparatorChar));
        var token = Guid.NewGuid().ToString("N");

        if (importType == ImportDataType.Teachers)
        {
            var teacherRows = await ReadTeacherRowsAsync(absolutePath, cancellationToken);
            var previewRows = new List<TeacherImportRowPreviewViewModel>();
            foreach (var row in teacherRows)
            {
                previewRows.Add(await BuildTeacherPreviewRowAsync(row, cancellationToken));
            }

            PreviewStore[token] = new ImportPreviewContext
            {
                ImportType = importType,
                Token = token,
                FileName = file.FileName,
                StoredRelativePath = storedPath,
                TeacherRows = teacherRows,
                TeacherPreviewRows = previewRows
            };

            return new StudentImportIndexViewModel
            {
                ImportType = importType,
                PreviewToken = token,
                FileName = file.FileName,
                TeacherRows = previewRows,
                TotalRows = previewRows.Count,
                ErrorRows = previewRows.Count(x => !x.IsValid),
                CanImport = previewRows.Any() && previewRows.All(x => x.IsValid)
            };
        }

        var rows = await ReadStudentRowsAsync(absolutePath, cancellationToken);
        var studentPreviewRows = rows.Select(BuildStudentPreviewRow).ToList();

        PreviewStore[token] = new ImportPreviewContext
        {
            ImportType = importType,
            Token = token,
            FileName = file.FileName,
            StoredRelativePath = storedPath,
            StudentRows = rows,
            StudentPreviewRows = studentPreviewRows
        };

        return new StudentImportIndexViewModel
        {
            ImportType = importType,
            PreviewToken = token,
            FileName = file.FileName,
            Rows = studentPreviewRows,
            TotalRows = studentPreviewRows.Count,
            ErrorRows = studentPreviewRows.Count(x => !x.IsValid),
            CanImport = studentPreviewRows.Any() && studentPreviewRows.All(x => x.IsValid)
        };
    }

    public async Task<StudentImportResultViewModel> ImportAsync(
        string previewToken,
        string? importedByUserId,
        string? ipAddress,
        CancellationToken cancellationToken = default)
    {
        if (!PreviewStore.TryGetValue(previewToken, out var context))
        {
            return new StudentImportResultViewModel
            {
                Errors = ["ไม่พบข้อมูลตัวอย่าง กรุณาอัปโหลดไฟล์ใหม่"]
            };
        }

        var result = context.ImportType == ImportDataType.Teachers
            ? await ImportTeachersAsync(context, cancellationToken)
            : await ImportStudentsAsync(context, importedByUserId, cancellationToken);

        await _auditLogService.LogAsync(
            importedByUserId,
            "Import",
            context.ImportType == ImportDataType.Teachers ? "Teachers" : "Students",
            result.BatchId > 0 ? result.BatchId.ToString(CultureInfo.InvariantCulture) : string.Empty,
            $"นำเข้า{(context.ImportType == ImportDataType.Teachers ? "ครู" : "นักเรียน")}: สำเร็จ={result.SuccessRows}, ผิดพลาด={result.FailedRows}",
            ipAddress,
            cancellationToken);

        PreviewStore.TryRemove(previewToken, out _);
        return result;
    }

    public byte[] GenerateTemplateWorkbook(ImportDataType importType)
    {
        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add(importType == ImportDataType.Teachers ? "Teachers" : "Students");

        if (importType == ImportDataType.Teachers)
        {
            var headers = new[] { "Username", "FullName", "Email", "Password", "Role", "Classroom" };
            WriteHeaderRow(worksheet, headers);
            WriteRow(worksheet, 2, ["teacher01", "ครูสมชาย ใจดี", "teacher01@school.local", "Teacher@123", "Teacher", "ม.1/1/1"]);
            WriteRow(worksheet, 3, ["homeroom01", "ครูกมล สุขดี", "homeroom01@school.local", "Teacher@123", "HomeroomHead", "ม.1/1/2"]);
        }
        else
        {
            var headers = new[]
            {
                "StudentCode", "Prefix", "FirstName", "LastName", "Gender", "BirthDate",
                "AcademicYear", "GradeLevel", "Classroom", "StudentNo", "GuardianName",
                "GuardianNationalId", "GuardianPhone", "NationalId", "Address", "Notes"
            };
            WriteHeaderRow(worksheet, headers);
            WriteRow(worksheet, 2, ["S0001", "ด.ช.", "ธนา", "ใจดี", "Male", "2012-05-01", "2568", "ม.1", "1/1", "1", "สมชาย ใจดี", "1234567890123", "0811111111", "1234567890123", "กรุงเทพฯ", "ตัวอย่าง"]);
            WriteRow(worksheet, 3, ["S0002", "ด.ญ.", "กมล", "สุขดี", "Female", "2012-08-12", "2568", "ม.1", "1/2", "2", "สมหญิง สุขดี", "", "0822222222", "", "นนทบุรี", ""]);
        }

        worksheet.Columns().AdjustToContents();
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private async Task<StudentImportResultViewModel> ImportStudentsAsync(
        ImportPreviewContext context,
        string? importedByUserId,
        CancellationToken cancellationToken)
    {
        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

        var batch = new ImportBatch
        {
            FileName = context.FileName,
            ImportedByUserId = importedByUserId,
            ImportedAt = DateTime.UtcNow,
            TotalRows = context.StudentRows.Count,
            Status = ImportBatchStatus.Pending
        };
        _dbContext.ImportBatches.Add(batch);
        await _dbContext.SaveChangesAsync(cancellationToken);

        var errors = new List<string>();
        var academicYearCache = await _dbContext.AcademicYears.ToDictionaryAsync(x => x.Name, cancellationToken);
        var gradeCache = await _dbContext.GradeLevels.ToDictionaryAsync(x => x.Name, cancellationToken);
        var classroomCache = await _dbContext.Classrooms
            .Include(x => x.AcademicYear)
            .Include(x => x.GradeLevel)
            .ToListAsync(cancellationToken);

        foreach (var row in context.StudentRows)
        {
            var preview = BuildStudentPreviewRow(row);
            var item = new ImportBatchItem
            {
                ImportBatchId = batch.Id,
                RowNumber = row.RowNumber,
                StudentCode = row.StudentCode
            };

            if (!preview.IsValid)
            {
                item.ResultStatus = ImportRowResultStatus.Failed;
                item.ErrorMessage = preview.ErrorMessage;
                batch.FailedRows++;
                _dbContext.ImportBatchItems.Add(item);
                continue;
            }

            try
            {
                var academicYear = await GetOrCreateAcademicYearAsync(row.AcademicYear, academicYearCache, cancellationToken);
                var gradeLevel = await GetOrCreateGradeLevelAsync(row.GradeLevel, gradeCache, cancellationToken);
                var classroom = await GetOrCreateClassroomAsync(row.Classroom, academicYear.Id, gradeLevel.Id, classroomCache, cancellationToken);

                var student = await _dbContext.Students.FirstOrDefaultAsync(x => x.StudentCode == row.StudentCode, cancellationToken);
                if (student is null)
                {
                    student = new Student { StudentCode = row.StudentCode, IsActive = true };
                    _dbContext.Students.Add(student);
                }

                student.Prefix = row.Prefix;
                student.FirstName = row.FirstName;
                student.LastName = row.LastName;
                student.Gender = ParseGender(row.Gender);
                student.BirthDate = ParseDate(row.BirthDate);
                student.AcademicYearId = academicYear.Id;
                student.GradeLevelId = gradeLevel.Id;
                student.ClassroomId = classroom.Id;
                student.StudentNo = BlankToNull(row.StudentNo);
                student.GuardianName = BlankToNull(row.GuardianName);
                student.GuardianNationalId = BlankToNull(row.GuardianNationalId);
                student.GuardianPhone = BlankToNull(row.GuardianPhone);
                student.NationalId = BlankToNull(row.NationalId);
                student.Address = BlankToNull(row.Address);
                student.Notes = BlankToNull(row.Notes);
                student.Status = StudentStatus.Active;

                await _dbContext.SaveChangesAsync(cancellationToken);

                if (!await _dbContext.FaceProfiles.AnyAsync(x => x.StudentId == student.Id, cancellationToken))
                {
                    _dbContext.FaceProfiles.Add(new FaceProfile
                    {
                        StudentId = student.Id,
                        EnrollmentStatus = EnrollmentStatus.NotRegistered,
                        TemplateVersion = "v1"
                    });
                    await _dbContext.SaveChangesAsync(cancellationToken);
                }

                item.ResultStatus = ImportRowResultStatus.Success;
                batch.SuccessRows++;
            }
            catch (Exception ex)
            {
                item.ResultStatus = ImportRowResultStatus.Failed;
                item.ErrorMessage = ex.Message;
                batch.FailedRows++;
                errors.Add($"แถวที่ {row.RowNumber}: {ex.Message}");
            }

            _dbContext.ImportBatchItems.Add(item);
        }

        batch.Status = batch.FailedRows == 0
            ? ImportBatchStatus.Completed
            : (batch.SuccessRows > 0 ? ImportBatchStatus.CompletedWithErrors : ImportBatchStatus.Failed);

        await _dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new StudentImportResultViewModel
        {
            BatchId = batch.Id,
            TotalRows = batch.TotalRows,
            SuccessRows = batch.SuccessRows,
            FailedRows = batch.FailedRows,
            Errors = errors
        };
    }

    private async Task<StudentImportResultViewModel> ImportTeachersAsync(ImportPreviewContext context, CancellationToken cancellationToken)
    {
        var errors = new List<string>();
        var successRows = 0;
        var failedRows = 0;

        foreach (var row in context.TeacherRows)
        {
            var preview = await BuildTeacherPreviewRowAsync(row, cancellationToken);
            if (!preview.IsValid)
            {
                failedRows++;
                errors.Add($"แถวที่ {row.RowNumber}: {preview.ErrorMessage}");
                continue;
            }

            try
            {
                var user = await _userManager.FindByNameAsync(row.Username);
                var assignedClassroomId = await ResolveClassroomIdAsync(row.Classroom, cancellationToken);

                if (user is null)
                {
                    user = new ApplicationUser
                    {
                        UserName = row.Username.Trim(),
                        Email = row.Email.Trim(),
                        EmailConfirmed = true,
                        FullName = row.FullName.Trim(),
                        AssignedClassroomId = assignedClassroomId,
                        IsActive = true
                    };

                    var createResult = await _userManager.CreateAsync(user, row.Password.Trim());
                    if (!createResult.Succeeded)
                    {
                        throw new InvalidOperationException(string.Join("; ", createResult.Errors.Select(x => x.Description)));
                    }
                }
                else
                {
                    user.Email = row.Email.Trim();
                    user.EmailConfirmed = true;
                    user.FullName = row.FullName.Trim();
                    user.AssignedClassroomId = assignedClassroomId;
                    user.IsActive = true;

                    var updateResult = await _userManager.UpdateAsync(user);
                    if (!updateResult.Succeeded)
                    {
                        throw new InvalidOperationException(string.Join("; ", updateResult.Errors.Select(x => x.Description)));
                    }

                    if (!string.IsNullOrWhiteSpace(row.Password))
                    {
                        var token = await _userManager.GeneratePasswordResetTokenAsync(user);
                        var resetResult = await _userManager.ResetPasswordAsync(user, token, row.Password.Trim());
                        if (!resetResult.Succeeded)
                        {
                            throw new InvalidOperationException(string.Join("; ", resetResult.Errors.Select(x => x.Description)));
                        }
                    }
                }

                var roles = await _userManager.GetRolesAsync(user);
                var targetRole = string.IsNullOrWhiteSpace(row.Role) ? "Teacher" : row.Role.Trim();
                if (!roles.Contains(targetRole, StringComparer.OrdinalIgnoreCase))
                {
                    if (roles.Count > 0)
                    {
                        await _userManager.RemoveFromRolesAsync(user, roles);
                    }

                    var roleResult = await _userManager.AddToRoleAsync(user, targetRole);
                    if (!roleResult.Succeeded)
                    {
                        throw new InvalidOperationException(string.Join("; ", roleResult.Errors.Select(x => x.Description)));
                    }
                }

                successRows++;
            }
            catch (Exception ex)
            {
                failedRows++;
                errors.Add($"แถวที่ {row.RowNumber}: {ex.Message}");
            }
        }

        return new StudentImportResultViewModel
        {
            BatchId = 0,
            TotalRows = context.TeacherRows.Count,
            SuccessRows = successRows,
            FailedRows = failedRows,
            Errors = errors
        };
    }

    private static StudentImportRowPreviewViewModel BuildStudentPreviewRow(StudentImportRow row)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(row.StudentCode)) errors.Add("StudentCode");
        if (string.IsNullOrWhiteSpace(row.Prefix)) errors.Add("Prefix");
        if (string.IsNullOrWhiteSpace(row.FirstName)) errors.Add("FirstName");
        if (string.IsNullOrWhiteSpace(row.LastName)) errors.Add("LastName");
        if (string.IsNullOrWhiteSpace(row.AcademicYear)) errors.Add("AcademicYear");
        if (string.IsNullOrWhiteSpace(row.GradeLevel)) errors.Add("GradeLevel");
        if (string.IsNullOrWhiteSpace(row.Classroom)) errors.Add("Classroom");

        return new StudentImportRowPreviewViewModel
        {
            RowNumber = row.RowNumber,
            StudentCode = row.StudentCode,
            Prefix = row.Prefix,
            FirstName = row.FirstName,
            LastName = row.LastName,
            Gender = row.Gender,
            BirthDate = row.BirthDate,
            AcademicYear = row.AcademicYear,
            GradeLevel = row.GradeLevel,
            Classroom = row.Classroom,
            StudentNo = row.StudentNo,
            GuardianName = row.GuardianName,
            GuardianNationalId = row.GuardianNationalId,
            GuardianPhone = row.GuardianPhone,
            NationalId = row.NationalId,
            Address = row.Address,
            Notes = row.Notes,
            IsValid = errors.Count == 0,
            ErrorMessage = errors.Count == 0 ? null : $"ข้อมูลไม่ครบ: {string.Join(", ", errors)}"
        };
    }

    private async Task<TeacherImportRowPreviewViewModel> BuildTeacherPreviewRowAsync(TeacherImportRow row, CancellationToken cancellationToken)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(row.Username)) errors.Add("Username");
        if (string.IsNullOrWhiteSpace(row.FullName)) errors.Add("FullName");
        if (string.IsNullOrWhiteSpace(row.Email)) errors.Add("Email");
        if (string.IsNullOrWhiteSpace(row.Password)) errors.Add("Password");
        if (!string.IsNullOrWhiteSpace(row.Role) && !await _roleManager.RoleExistsAsync(row.Role.Trim())) errors.Add("Role ไม่มีในระบบ");
        if (!string.IsNullOrWhiteSpace(row.Email) && await EmailBelongsToOtherUserAsync(row.Email, row.Username, cancellationToken)) errors.Add("Email ซ้ำ");
        if (!string.IsNullOrWhiteSpace(row.Classroom) && await ResolveClassroomIdAsync(row.Classroom, cancellationToken) is null) errors.Add("Classroom ไม่พบ");

        return new TeacherImportRowPreviewViewModel
        {
            RowNumber = row.RowNumber,
            Username = row.Username,
            FullName = row.FullName,
            Email = row.Email,
            Role = string.IsNullOrWhiteSpace(row.Role) ? "Teacher" : row.Role,
            Classroom = row.Classroom,
            IsValid = errors.Count == 0,
            ErrorMessage = errors.Count == 0 ? null : string.Join(", ", errors)
        };
    }

    private async Task<bool> EmailBelongsToOtherUserAsync(string email, string username, CancellationToken cancellationToken)
    {
        return await _dbContext.Users
            .AsNoTracking()
            .AnyAsync(x => x.Email == email.Trim() && x.UserName != username.Trim(), cancellationToken);
    }

    private async Task<List<StudentImportRow>> ReadStudentRowsAsync(string filePath, CancellationToken cancellationToken)
    {
        return IsExcelFile(filePath)
            ? ReadStudentWorkbookRows(filePath)
            : await ReadStudentCsvRowsAsync(filePath, cancellationToken);
    }

    private async Task<List<TeacherImportRow>> ReadTeacherRowsAsync(string filePath, CancellationToken cancellationToken)
    {
        return IsExcelFile(filePath)
            ? ReadTeacherWorkbookRows(filePath)
            : await ReadTeacherCsvRowsAsync(filePath, cancellationToken);
    }

    private static List<StudentImportRow> ReadStudentWorkbookRows(string filePath)
    {
        using var workbook = new XLWorkbook(filePath);
        var worksheet = workbook.Worksheets.First();
        var headers = ReadWorkbookHeaders(worksheet);
        var rows = new List<StudentImportRow>();

        foreach (var row in worksheet.RowsUsed().Skip(1))
        {
            rows.Add(new StudentImportRow
            {
                RowNumber = row.RowNumber(),
                StudentCode = Cell(row, headers, "StudentCode"),
                Prefix = Cell(row, headers, "Prefix"),
                FirstName = Cell(row, headers, "FirstName"),
                LastName = Cell(row, headers, "LastName"),
                Gender = Cell(row, headers, "Gender"),
                BirthDate = Cell(row, headers, "BirthDate"),
                AcademicYear = Cell(row, headers, "AcademicYear"),
                GradeLevel = Cell(row, headers, "GradeLevel"),
                Classroom = Cell(row, headers, "Classroom"),
                StudentNo = Cell(row, headers, "StudentNo"),
                GuardianName = Cell(row, headers, "GuardianName"),
                GuardianNationalId = Cell(row, headers, "GuardianNationalId"),
                GuardianPhone = Cell(row, headers, "GuardianPhone"),
                NationalId = Cell(row, headers, "NationalId"),
                Address = Cell(row, headers, "Address"),
                Notes = Cell(row, headers, "Notes")
            });
        }

        return rows;
    }

    private static List<TeacherImportRow> ReadTeacherWorkbookRows(string filePath)
    {
        using var workbook = new XLWorkbook(filePath);
        var worksheet = workbook.Worksheets.First();
        var headers = ReadWorkbookHeaders(worksheet);
        var rows = new List<TeacherImportRow>();

        foreach (var row in worksheet.RowsUsed().Skip(1))
        {
            rows.Add(new TeacherImportRow
            {
                RowNumber = row.RowNumber(),
                Username = Cell(row, headers, "Username"),
                FullName = Cell(row, headers, "FullName"),
                Email = Cell(row, headers, "Email"),
                Password = Cell(row, headers, "Password"),
                Role = Cell(row, headers, "Role"),
                Classroom = Cell(row, headers, "Classroom")
            });
        }

        return rows;
    }

    private async Task<List<StudentImportRow>> ReadStudentCsvRowsAsync(string filePath, CancellationToken cancellationToken)
    {
        var rows = new List<StudentImportRow>();
        using var csv = OpenCsv(filePath);
        await csv.ReadAsync();
        csv.ReadHeader();

        var rowNumber = 1;
        while (await csv.ReadAsync())
        {
            cancellationToken.ThrowIfCancellationRequested();
            rowNumber++;
            rows.Add(new StudentImportRow
            {
                RowNumber = rowNumber,
                StudentCode = CsvCell(csv, "StudentCode"),
                Prefix = CsvCell(csv, "Prefix"),
                FirstName = CsvCell(csv, "FirstName"),
                LastName = CsvCell(csv, "LastName"),
                Gender = CsvCell(csv, "Gender"),
                BirthDate = CsvCell(csv, "BirthDate"),
                AcademicYear = CsvCell(csv, "AcademicYear"),
                GradeLevel = CsvCell(csv, "GradeLevel"),
                Classroom = CsvCell(csv, "Classroom"),
                StudentNo = CsvCell(csv, "StudentNo"),
                GuardianName = CsvCell(csv, "GuardianName"),
                GuardianNationalId = CsvCell(csv, "GuardianNationalId"),
                GuardianPhone = CsvCell(csv, "GuardianPhone"),
                NationalId = CsvCell(csv, "NationalId"),
                Address = CsvCell(csv, "Address"),
                Notes = CsvCell(csv, "Notes")
            });
        }

        return rows;
    }

    private async Task<List<TeacherImportRow>> ReadTeacherCsvRowsAsync(string filePath, CancellationToken cancellationToken)
    {
        var rows = new List<TeacherImportRow>();
        using var csv = OpenCsv(filePath);
        await csv.ReadAsync();
        csv.ReadHeader();

        var rowNumber = 1;
        while (await csv.ReadAsync())
        {
            cancellationToken.ThrowIfCancellationRequested();
            rowNumber++;
            rows.Add(new TeacherImportRow
            {
                RowNumber = rowNumber,
                Username = CsvCell(csv, "Username"),
                FullName = CsvCell(csv, "FullName"),
                Email = CsvCell(csv, "Email"),
                Password = CsvCell(csv, "Password"),
                Role = CsvCell(csv, "Role"),
                Classroom = CsvCell(csv, "Classroom")
            });
        }

        return rows;
    }

    private async Task<AcademicYear> GetOrCreateAcademicYearAsync(string name, IDictionary<string, AcademicYear> cache, CancellationToken cancellationToken)
    {
        var key = name.Trim();
        if (cache.TryGetValue(key, out var value)) return value;

        var year = new AcademicYear
        {
            Name = key,
            StartDate = DateTime.UtcNow.Date,
            EndDate = DateTime.UtcNow.Date.AddYears(1).AddDays(-1),
            IsCurrent = false,
            IsActive = true
        };

        _dbContext.AcademicYears.Add(year);
        await _dbContext.SaveChangesAsync(cancellationToken);
        cache[key] = year;
        return year;
    }

    private async Task<GradeLevel> GetOrCreateGradeLevelAsync(string name, IDictionary<string, GradeLevel> cache, CancellationToken cancellationToken)
    {
        var key = name.Trim();
        if (cache.TryGetValue(key, out var value)) return value;

        var grade = new GradeLevel
        {
            Name = key,
            SortOrder = cache.Count == 0 ? 1 : cache.Values.Max(x => x.SortOrder) + 1,
            IsActive = true
        };

        _dbContext.GradeLevels.Add(grade);
        await _dbContext.SaveChangesAsync(cancellationToken);
        cache[key] = grade;
        return grade;
    }

    private async Task<Classroom> GetOrCreateClassroomAsync(
        string name,
        int academicYearId,
        int gradeLevelId,
        IList<Classroom> cache,
        CancellationToken cancellationToken)
    {
        var key = name.Trim();
        var found = cache.FirstOrDefault(x => x.Name == key && x.AcademicYearId == academicYearId && x.GradeLevelId == gradeLevelId);
        if (found is not null) return found;

        var classroom = new Classroom
        {
            Name = key,
            RoomCode = key,
            AcademicYearId = academicYearId,
            GradeLevelId = gradeLevelId,
            IsActive = true
        };

        _dbContext.Classrooms.Add(classroom);
        await _dbContext.SaveChangesAsync(cancellationToken);
        cache.Add(classroom);
        return classroom;
    }

    private async Task<int?> ResolveClassroomIdAsync(string? classroomText, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(classroomText)) return null;

        var value = classroomText.Trim();
        var classrooms = await _dbContext.Classrooms
            .AsNoTracking()
            .Include(x => x.GradeLevel)
            .Where(x => x.IsActive)
            .ToListAsync(cancellationToken);

        var exact = classrooms.FirstOrDefault(x =>
            string.Equals(x.Name, value, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(x.RoomCode, value, StringComparison.OrdinalIgnoreCase) ||
            string.Equals($"{x.GradeLevel!.Name}/{x.Name}", value, StringComparison.OrdinalIgnoreCase));

        return exact?.Id;
    }

    private static GenderType ParseGender(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return GenderType.Unknown;
        var text = value.Trim().ToLowerInvariant();

        return text switch
        {
            "male" or "m" or "ชาย" => GenderType.Male,
            "female" or "f" or "หญิง" => GenderType.Female,
            _ => GenderType.Unknown
        };
    }

    private static DateTime? ParseDate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;

        var formats = new[] { "yyyy-MM-dd", "dd/MM/yyyy", "MM/dd/yyyy" };
        return DateTime.TryParseExact(value.Trim(), formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
            ? date
            : DateTime.TryParse(value, out date) ? date : null;
    }

    private static CsvReader OpenCsv(string filePath)
    {
        var reader = new StreamReader(File.OpenRead(filePath));
        return new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            MissingFieldFound = null,
            HeaderValidated = null,
            BadDataFound = null,
            TrimOptions = TrimOptions.Trim
        });
    }

    private static Dictionary<string, int> ReadWorkbookHeaders(IXLWorksheet worksheet)
    {
        return worksheet.Row(1).CellsUsed()
            .Where(x => !string.IsNullOrWhiteSpace(x.GetString()))
            .ToDictionary(x => x.GetString().Trim(), x => x.Address.ColumnNumber, StringComparer.OrdinalIgnoreCase);
    }

    private static string Cell(IXLRow row, IReadOnlyDictionary<string, int> headers, string header)
        => headers.TryGetValue(header, out var column) ? row.Cell(column).GetFormattedString().Trim() : string.Empty;

    private static string CsvCell(CsvReader csv, string header)
    {
        try
        {
            return csv.GetField(header)?.Trim() ?? string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    private static bool IsExcelFile(string filePath)
        => string.Equals(Path.GetExtension(filePath), ".xlsx", StringComparison.OrdinalIgnoreCase);

    private static string? BlankToNull(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static void WriteHeaderRow(IXLWorksheet worksheet, IReadOnlyList<string> headers)
    {
        for (var i = 0; i < headers.Count; i++)
        {
            worksheet.Cell(1, i + 1).Value = headers[i];
        }

        worksheet.Row(1).Style.Font.Bold = true;
    }

    private static void WriteRow(IXLWorksheet worksheet, int rowNumber, IReadOnlyList<string> values)
    {
        for (var i = 0; i < values.Count; i++)
        {
            worksheet.Cell(rowNumber, i + 1).Value = values[i];
        }
    }

    private sealed class ImportPreviewContext
    {
        public ImportDataType ImportType { get; set; }
        public string Token { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
        public string StoredRelativePath { get; set; } = string.Empty;
        public List<StudentImportRow> StudentRows { get; set; } = [];
        public List<StudentImportRowPreviewViewModel> StudentPreviewRows { get; set; } = [];
        public List<TeacherImportRow> TeacherRows { get; set; } = [];
        public List<TeacherImportRowPreviewViewModel> TeacherPreviewRows { get; set; } = [];
    }

    private sealed class StudentImportRow
    {
        public int RowNumber { get; set; }
        public string StudentCode { get; set; } = string.Empty;
        public string Prefix { get; set; } = string.Empty;
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string Gender { get; set; } = string.Empty;
        public string BirthDate { get; set; } = string.Empty;
        public string AcademicYear { get; set; } = string.Empty;
        public string GradeLevel { get; set; } = string.Empty;
        public string Classroom { get; set; } = string.Empty;
        public string StudentNo { get; set; } = string.Empty;
        public string GuardianName { get; set; } = string.Empty;
        public string GuardianNationalId { get; set; } = string.Empty;
        public string GuardianPhone { get; set; } = string.Empty;
        public string NationalId { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public string Notes { get; set; } = string.Empty;
    }

    private sealed class TeacherImportRow
    {
        public int RowNumber { get; set; }
        public string Username { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public string Classroom { get; set; } = string.Empty;
    }
}
