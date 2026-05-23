namespace FaceScan.Web.ViewModels.TeacherAttendance;

public class TeacherAttendanceTransactionRowViewModel
{
    public DateTime ScanTime { get; set; }
    public string Username { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string RoleName { get; set; } = string.Empty;
    public string AssignedClassroomName { get; set; } = string.Empty;
    public string ScanType { get; set; } = string.Empty;
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }
    public decimal? LocationAccuracyMeters { get; set; }
    public string? MapUrl { get; set; }
    public bool IsDuplicate { get; set; }
    public decimal Confidence { get; set; }
    public string Provider { get; set; } = string.Empty;
}
