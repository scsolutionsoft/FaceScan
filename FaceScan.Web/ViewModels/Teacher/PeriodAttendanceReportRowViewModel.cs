namespace FaceScan.Web.ViewModels.Teacher;

public class PeriodAttendanceReportRowViewModel
{
    public int StudentId { get; set; }
    public string StudentCode { get; set; } = string.Empty;
    public string StudentName { get; set; } = string.Empty;
    public IReadOnlyList<PeriodAttendanceReportCellViewModel> Periods { get; set; } = [];
}
