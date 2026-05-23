namespace FaceScan.Web.ViewModels.Executive;

public class ExecutiveRiskTrendPointViewModel
{
    public DateTime Date { get; set; }
    public int PresentCount { get; set; }
    public int AbsentCount { get; set; }
    public decimal AttendanceRate { get; set; }
    public string RiskLevel { get; set; } = string.Empty;
}