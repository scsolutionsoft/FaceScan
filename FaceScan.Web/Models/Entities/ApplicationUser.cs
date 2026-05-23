using Microsoft.AspNetCore.Identity;

namespace FaceScan.Web.Models.Entities;

public class ApplicationUser : IdentityUser
{
    public string FullName { get; set; } = string.Empty;
    public int? StudentId { get; set; }
    public int? AssignedClassroomId { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Student? Student { get; set; }
    public Classroom? AssignedClassroom { get; set; }
    public ICollection<PeriodAttendanceSession> PeriodAttendanceSessions { get; set; } = new List<PeriodAttendanceSession>();
    public TeacherFaceProfile? TeacherFaceProfile { get; set; }
    public ICollection<TeacherFacePhoto> TeacherFacePhotos { get; set; } = new List<TeacherFacePhoto>();
    public ICollection<TeacherAttendanceTransaction> TeacherAttendanceTransactions { get; set; } = new List<TeacherAttendanceTransaction>();
    public ICollection<TeacherAttendanceDailySummary> TeacherAttendanceDailySummaries { get; set; } = new List<TeacherAttendanceDailySummary>();
}
