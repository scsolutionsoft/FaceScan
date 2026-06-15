using FaceScan.Web.Models.Enums;

namespace FaceScan.Web.Models.Entities;

public class Student : BaseEntity
{
    public string StudentCode { get; set; } = string.Empty;
    public string? NationalId { get; set; }
    public string Prefix { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string? FirstNameEn { get; set; }
    public string? LastNameEn { get; set; }
    public GenderType Gender { get; set; }
    public DateTime? BirthDate { get; set; }
    public int AcademicYearId { get; set; }
    public int GradeLevelId { get; set; }
    public int ClassroomId { get; set; }
    public string? RoomNumber { get; set; }
    public string? StudentNo { get; set; }
    public StudentStatus Status { get; set; } = StudentStatus.Active;
    public string? GuardianName { get; set; }
    public string? GuardianNationalId { get; set; }
    public string? GuardianPhone { get; set; }
    public string? Address { get; set; }
    public string? Notes { get; set; }
    public bool IsActive { get; set; } = true;

    public AcademicYear? AcademicYear { get; set; }
    public GradeLevel? GradeLevel { get; set; }
    public Classroom? Classroom { get; set; }
    public FaceProfile? FaceProfile { get; set; }
    public ICollection<StudentPhoto> StudentPhotos { get; set; } = new List<StudentPhoto>();
    public ICollection<AttendanceTransaction> AttendanceTransactions { get; set; } = new List<AttendanceTransaction>();
    public ICollection<AttendanceDailySummary> AttendanceDailySummaries { get; set; } = new List<AttendanceDailySummary>();
    public ICollection<PeriodAttendanceRecord> PeriodAttendanceRecords { get; set; } = new List<PeriodAttendanceRecord>();

    public string FullName => $"{Prefix}{FirstName} {LastName}".Trim();
}
