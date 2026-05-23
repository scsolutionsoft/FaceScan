using FaceScan.Web.Models.Enums;

namespace FaceScan.Web.Models.Entities;

public class PeriodAttendanceSession : BaseEntity
{
    public DateTime Date { get; set; }
    public int ClassroomId { get; set; }
    public int ClassPeriodId { get; set; }
    public string CheckedByUserId { get; set; } = string.Empty;
    public DateTime CheckedAt { get; set; } = DateTime.UtcNow;
    public TeacherTeachingStatus TeacherStatus { get; set; } = TeacherTeachingStatus.Normal;
    public string? TeacherStatusNote { get; set; }

    public Classroom? Classroom { get; set; }
    public ClassPeriod? ClassPeriod { get; set; }
    public ApplicationUser? CheckedByUser { get; set; }
    public ICollection<PeriodAttendanceRecord> Records { get; set; } = new List<PeriodAttendanceRecord>();
}
