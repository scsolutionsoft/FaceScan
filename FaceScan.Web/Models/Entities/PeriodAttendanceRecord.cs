using FaceScan.Web.Models.Enums;

namespace FaceScan.Web.Models.Entities;

public class PeriodAttendanceRecord : BaseEntity
{
    public int PeriodAttendanceSessionId { get; set; }
    public int StudentId { get; set; }
    public PeriodAttendanceStatus Status { get; set; } = PeriodAttendanceStatus.Present;
    public string? Remark { get; set; }

    public PeriodAttendanceSession? PeriodAttendanceSession { get; set; }
    public Student? Student { get; set; }
}
