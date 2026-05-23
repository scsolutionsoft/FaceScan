using FaceScan.Web.Models.Enums;

namespace FaceScan.Web.Models.Entities;

public class AttendanceDailySummary : BaseEntity
{
    public int StudentId { get; set; }
    public int? ClassroomId { get; set; }
    public DateTime Date { get; set; }
    public DateTime? FirstCheckInTime { get; set; }
    public DateTime? LastCheckOutTime { get; set; }
    public AttendanceStatus AttendanceStatus { get; set; }
    public int TotalScans { get; set; }
    public bool IsPresent { get; set; }
    public string? Remark { get; set; }

    public Student? Student { get; set; }
    public Classroom? Classroom { get; set; }
}
