namespace FaceScan.Web.ViewModels.Attendance;

public class AttendanceTransactionRowViewModel
{
    public DateTime ScanTime { get; set; }
    public string StudentCode { get; set; } = string.Empty;
    public string StudentName { get; set; } = string.Empty;
    public string ScanType { get; set; } = string.Empty;
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }
    public decimal? LocationAccuracyMeters { get; set; }
    public string? MapUrl { get; set; }
    public bool IsDuplicate { get; set; }
    public decimal Confidence { get; set; }
    public string Provider { get; set; } = string.Empty;
}
