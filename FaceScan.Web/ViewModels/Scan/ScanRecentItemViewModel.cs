using FaceScan.Web.Models.Enums;

namespace FaceScan.Web.ViewModels.Scan;

public class ScanRecentItemViewModel
{
    public int TransactionId { get; set; }
    public DateTime ScanTime { get; set; }
    public ScanType ScanType { get; set; }
    public string StudentCode { get; set; } = string.Empty;
    public string StudentName { get; set; } = string.Empty;
    public string GradeLevel { get; set; } = string.Empty;
    public string Classroom { get; set; } = string.Empty;
    public decimal Confidence { get; set; }
    public string Provider { get; set; } = string.Empty;
}
