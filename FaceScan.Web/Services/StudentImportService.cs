using System.Collections.Concurrent;
using System.Globalization;
using CsvHelper;
using CsvHelper.Configuration;
using FaceScan.Web.Data;
using FaceScan.Web.Models.Entities;
using FaceScan.Web.Models.Enums;
using FaceScan.Web.Services.Interfaces;
using FaceScan.Web.ViewModels.Import;
using Microsoft.EntityFrameworkCore;

namespace FaceScan.Web.Services;

public class StudentImportService : IStudentImportService
{
    private static readonly ConcurrentDictionary<string, ImportPreviewContext> PreviewStore = new();

    private readonly ApplicationDbContext _dbContext;
    private readonly IFileStorageService _fileStorageService;
    private readonly IAuditLogService _auditLogService;
    private readonly IWebHostEnvironment _environment;

    public StudentImportService(
        ApplicationDbContext dbContext,
        IFileStorageService fileStorageService,
        IAuditLogService auditLogService,
        IWebHostEnvironment environment)
    {
        _dbContext = dbContext;
        _fileStorageService = fileStorageService;
        _auditLogService = auditLogService;
        _environment = environment;
    }

    public async Task<StudentImportIndexViewModel> PreviewAsync(IFormFile file, CancellationToken cancellationToken = default)
    {
        var storedPath = await _fileStorageService.SaveImportFileAsync(file, cancellationToken);
        var absolutePath = Path.Combine(_environment.WebRootPath, storedPath.Replace('/', Path.DirectorySeparatorChar));

        var rows = await ReadCsvRowsAsync(absolutePath, cancellationToken);
        var previewRows = rows.Select(BuildPreviewRow).ToList();

        var token = Guid.NewGuid().ToString("N");
        PreviewStore[token] = new ImportPreviewContext
        {
            Token = token,
            FileName = file.FileName,
            StoredRelativePath = storedPath,
            Rows = rows,
            PreviewRows = previewRows
        };

        return new StudentImportIndexViewModel
        {
            PreviewToken = token,
            FileName = file.FileName,
            Rows = previewRows,
            TotalRows = previewRows.Count,
            ErrorRows = previewRows.Count(x => !x.IsValid),
            CanImport = previewRows.Any() && previewRows.All(x => x.IsValid)
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

        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

        var batch = new ImportBatch
        {
            FileName = context.FileName,
            ImportedByUserId = importedByUserId,
            ImportedAt = DateTime.UtcNow,
            TotalRows = context.Rows.Count,
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

        foreach (var row in context.Rows)
        {
            var preview = BuildPreviewRow(row);
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

                var student = await _dbContext.Students
                    .FirstOrDefaultAsync(x => x.StudentCode == row.StudentCode, cancellationToken);

                if (student is null)
                {
                    student = new Student
                    {
                        StudentCode = row.StudentCode,
                        IsActive = true
                    };
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
                student.StudentNo = string.IsNullOrWhiteSpace(row.StudentNo) ? null : row.StudentNo;
                student.GuardianName = string.IsNullOrWhiteSpace(row.GuardianName) ? null : row.GuardianName;
                student.GuardianPhone = string.IsNullOrWhiteSpace(row.GuardianPhone) ? null : row.GuardianPhone;
                student.NationalId = string.IsNullOrWhiteSpace(row.NationalId) ? null : row.NationalId;
                student.Address = string.IsNullOrWhiteSpace(row.Address) ? null : row.Address;
                student.Notes = string.IsNullOrWhiteSpace(row.Notes) ? null : row.Notes;
                student.Status = StudentStatus.Active;

                await _dbContext.SaveChangesAsync(cancellationToken);

                var faceProfile = await _dbContext.FaceProfiles.FirstOrDefaultAsync(x => x.StudentId == student.Id, cancellationToken);
                if (faceProfile is null)
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

        await _auditLogService.LogAsync(
            importedByUserId,
            "Import",
            "Students",
            batch.Id.ToString(),
            $"นำเข้านักเรียน: สำเร็จ={batch.SuccessRows}, ผิดพลาด={batch.FailedRows}",
            ipAddress,
            cancellationToken);

        PreviewStore.TryRemove(previewToken, out _);

        return new StudentImportResultViewModel
        {
            BatchId = batch.Id,
            TotalRows = batch.TotalRows,
            SuccessRows = batch.SuccessRows,
            FailedRows = batch.FailedRows,
            Errors = errors
        };
    }

    public byte[] GenerateTemplateCsv()
    {
        const string header = "StudentCode,Prefix,FirstName,LastName,Gender,BirthDate,AcademicYear,GradeLevel,Classroom,StudentNo,GuardianName,GuardianPhone,NationalId,Address,Notes";
        const string sample1 = "S0001,ด.ช.,ธนา,ใจดี,Male,2012-05-01,2568,ม.1,1/1,1,สมชาย,0811111111,1234567890123,กรุงเทพฯ,ตัวอย่าง";
        const string sample2 = "S0002,ด.ญ.,กมล,สุขดี,Female,2012-08-12,2568,ม.1,1/2,2,สมหญิง,0822222222,,นนทบุรี,";

        var csv = string.Join(Environment.NewLine, new[] { header, sample1, sample2 });
        return System.Text.Encoding.UTF8.GetBytes(csv);
    }

    private static StudentImportRowPreviewViewModel BuildPreviewRow(ImportCsvRow row)
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
            GuardianPhone = row.GuardianPhone,
            NationalId = row.NationalId,
            Address = row.Address,
            Notes = row.Notes,
            IsValid = errors.Count == 0,
            ErrorMessage = errors.Count == 0 ? null : $"ข้อมูลไม่ครบ: {string.Join(",", errors)}"
        };
    }

    private async Task<List<ImportCsvRow>> ReadCsvRowsAsync(string filePath, CancellationToken cancellationToken)
    {
        var rows = new List<ImportCsvRow>();
        await using var stream = File.OpenRead(filePath);
        using var reader = new StreamReader(stream);
        using var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            MissingFieldFound = null,
            HeaderValidated = null,
            BadDataFound = null,
            TrimOptions = TrimOptions.Trim
        });

        await csv.ReadAsync();
        csv.ReadHeader();

        var rowNumber = 1;
        while (await csv.ReadAsync())
        {
            cancellationToken.ThrowIfCancellationRequested();
            rowNumber++;

            rows.Add(new ImportCsvRow
            {
                RowNumber = rowNumber,
                StudentCode = csv.GetField("StudentCode") ?? string.Empty,
                Prefix = csv.GetField("Prefix") ?? string.Empty,
                FirstName = csv.GetField("FirstName") ?? string.Empty,
                LastName = csv.GetField("LastName") ?? string.Empty,
                Gender = csv.GetField("Gender") ?? string.Empty,
                BirthDate = csv.GetField("BirthDate") ?? string.Empty,
                AcademicYear = csv.GetField("AcademicYear") ?? string.Empty,
                GradeLevel = csv.GetField("GradeLevel") ?? string.Empty,
                Classroom = csv.GetField("Classroom") ?? string.Empty,
                StudentNo = csv.GetField("StudentNo") ?? string.Empty,
                GuardianName = csv.GetField("GuardianName") ?? string.Empty,
                GuardianPhone = csv.GetField("GuardianPhone") ?? string.Empty,
                NationalId = csv.GetField("NationalId") ?? string.Empty,
                Address = csv.GetField("Address") ?? string.Empty,
                Notes = csv.GetField("Notes") ?? string.Empty
            });
        }

        return rows;
    }

    private async Task<AcademicYear> GetOrCreateAcademicYearAsync(
        string name,
        IDictionary<string, AcademicYear> cache,
        CancellationToken cancellationToken)
    {
        var key = name.Trim();
        if (cache.TryGetValue(key, out var value))
        {
            return value;
        }

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

    private async Task<GradeLevel> GetOrCreateGradeLevelAsync(
        string name,
        IDictionary<string, GradeLevel> cache,
        CancellationToken cancellationToken)
    {
        var key = name.Trim();
        if (cache.TryGetValue(key, out var value))
        {
            return value;
        }

        var maxSortOrder = cache.Count == 0 ? 0 : cache.Values.Max(x => x.SortOrder);
        var grade = new GradeLevel
        {
            Name = key,
            SortOrder = maxSortOrder + 1,
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
        if (found is not null)
        {
            return found;
        }

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
        if (DateTime.TryParseExact(value.Trim(), formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
        {
            return date;
        }

        return DateTime.TryParse(value, out date) ? date : null;
    }

    private sealed class ImportPreviewContext
    {
        public string Token { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
        public string StoredRelativePath { get; set; } = string.Empty;
        public List<ImportCsvRow> Rows { get; set; } = [];
        public List<StudentImportRowPreviewViewModel> PreviewRows { get; set; } = [];
    }

    private sealed class ImportCsvRow
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
        public string GuardianPhone { get; set; } = string.Empty;
        public string NationalId { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public string Notes { get; set; } = string.Empty;
    }
}
