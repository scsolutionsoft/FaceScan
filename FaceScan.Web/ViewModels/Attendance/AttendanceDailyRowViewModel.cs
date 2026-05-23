using FaceScan.Web.Models.Enums;

namespace FaceScan.Web.ViewModels.Attendance;

public class AttendanceDailyRowViewModel
{
    public int StudentId { get; set; }
    public string StudentCode { get; set; } = string.Empty;
    public string StudentName { get; set; } = string.Empty;
    public string GradeLevel { get; set; } = string.Empty;
    public string Classroom { get; set; } = string.Empty;
    public DateTime? FirstCheckInTime { get; set; }
    public DateTime? LastCheckOutTime { get; set; }
    public decimal? CheckInLatitude { get; set; }
    public decimal? CheckInLongitude { get; set; }
    public decimal? CheckOutLatitude { get; set; }
    public decimal? CheckOutLongitude { get; set; }
    public string? CheckInMapUrl { get; set; }
    public string? CheckOutMapUrl { get; set; }
    public AttendanceStatus AttendanceStatus { get; set; }
    public string StatusText { get; set; } = string.Empty;
    public string StatusLabel { get; set; } = string.Empty;
    public int StatusSortOrder { get; set; }
    public bool IsLate { get; set; }
    public bool IsPresent { get; set; }
}
