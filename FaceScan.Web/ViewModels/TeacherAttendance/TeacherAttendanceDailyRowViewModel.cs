using FaceScan.Web.Models.Enums;

namespace FaceScan.Web.ViewModels.TeacherAttendance;

public class TeacherAttendanceDailyRowViewModel
{
    public string UserId { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string RoleName { get; set; } = string.Empty;
    public string AssignedClassroomName { get; set; } = string.Empty;
    public DateTime? FirstCheckInTime { get; set; }
    public DateTime? LastCheckOutTime { get; set; }
    public AttendanceStatus AttendanceStatus { get; set; }
    public string StatusText { get; set; } = string.Empty;
    public string StatusLabel { get; set; } = string.Empty;
    public int StatusSortOrder { get; set; }
    public bool IsPresent { get; set; }
    public bool IsLate { get; set; }
    public decimal? CheckInLatitude { get; set; }
    public decimal? CheckInLongitude { get; set; }
    public string? CheckInMapUrl { get; set; }
    public decimal? CheckOutLatitude { get; set; }
    public decimal? CheckOutLongitude { get; set; }
    public string? CheckOutMapUrl { get; set; }
}
