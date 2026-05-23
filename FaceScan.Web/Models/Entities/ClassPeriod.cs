namespace FaceScan.Web.Models.Entities;

public class ClassPeriod : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public TimeSpan? StartTime { get; set; }
    public TimeSpan? EndTime { get; set; }
    public bool IsVisibleForCheck { get; set; } = true;
    public bool IsActive { get; set; } = true;

    public ICollection<PeriodAttendanceSession> PeriodAttendanceSessions { get; set; } = new List<PeriodAttendanceSession>();
}
