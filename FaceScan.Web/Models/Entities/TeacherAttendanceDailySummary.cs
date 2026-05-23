using FaceScan.Web.Models.Enums;

namespace FaceScan.Web.Models.Entities;

public class TeacherAttendanceDailySummary : BaseEntity
{
    public string UserId { get; set; } = string.Empty;
    public DateTime Date { get; set; }
    public DateTime? FirstCheckInTime { get; set; }
    public DateTime? LastCheckOutTime { get; set; }
    public AttendanceStatus AttendanceStatus { get; set; }
    public int TotalScans { get; set; }
    public bool IsPresent { get; set; }
    public string? Remark { get; set; }

    public ApplicationUser? User { get; set; }
}
