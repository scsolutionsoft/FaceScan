using FaceScan.Web.Models.Enums;

namespace FaceScan.Web.ViewModels.StudentPortal;

public class StudentAttendanceReportRowViewModel
{
    public DateTime Date { get; set; }
    public DateTime? FirstCheckInTime { get; set; }
    public DateTime? LastCheckOutTime { get; set; }
    public decimal? CheckInLatitude { get; set; }
    public decimal? CheckInLongitude { get; set; }
    public decimal? CheckOutLatitude { get; set; }
    public decimal? CheckOutLongitude { get; set; }
    public string? CheckInMapUrl { get; set; }
    public string? CheckOutMapUrl { get; set; }
    public AttendanceStatus AttendanceStatus { get; set; }
    public string StatusLabel { get; set; } = string.Empty;
    public bool IsLate { get; set; }
}
