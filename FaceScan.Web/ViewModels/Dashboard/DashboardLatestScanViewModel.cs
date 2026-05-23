namespace FaceScan.Web.ViewModels.Dashboard;

public class DashboardLatestScanViewModel
{
    public DateTime ScanTime { get; set; }
    public string StudentCode { get; set; } = string.Empty;
    public string StudentName { get; set; } = string.Empty;
    public string ScanType { get; set; } = string.Empty;
}
