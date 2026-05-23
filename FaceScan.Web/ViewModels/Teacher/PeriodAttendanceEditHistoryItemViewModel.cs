namespace FaceScan.Web.ViewModels.Teacher;

public class PeriodAttendanceEditHistoryItemViewModel
{
    public DateTime LoggedAt { get; set; }
    public string ActionLabel { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public string Detail { get; set; } = string.Empty;
}
