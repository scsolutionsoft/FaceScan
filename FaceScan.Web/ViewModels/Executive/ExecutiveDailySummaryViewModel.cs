namespace FaceScan.Web.ViewModels.Executive;

public class ExecutiveDailySummaryViewModel
{
    public DateTime Date { get; set; }
    public int PresentCount { get; set; }
    public int AbsentCount { get; set; }
    public int LeaveCount { get; set; }
    public int LateCount { get; set; }
    public int TruancyCount { get; set; }
    public int OtherCount { get; set; }
}
