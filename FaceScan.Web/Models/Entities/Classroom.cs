namespace FaceScan.Web.Models.Entities;

public class Classroom : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string? RoomCode { get; set; }
    public int AcademicYearId { get; set; }
    public int GradeLevelId { get; set; }
    public bool IsActive { get; set; } = true;

    public AcademicYear? AcademicYear { get; set; }
    public GradeLevel? GradeLevel { get; set; }
    public ICollection<Student> Students { get; set; } = new List<Student>();
    public ICollection<ApplicationUser> AssignedUsers { get; set; } = new List<ApplicationUser>();
    public ICollection<PeriodAttendanceSession> PeriodAttendanceSessions { get; set; } = new List<PeriodAttendanceSession>();
}
