using FaceScan.Web.Models.Enums;

namespace FaceScan.Web.ViewModels.Teacher;

public class PeriodAttendanceReportCellViewModel
{
    public int ClassPeriodId { get; set; }
    public string ClassPeriodName { get; set; } = string.Empty;
    public PeriodAttendanceStatus? Status { get; set; }
    public bool IsCurrentPeriod { get; set; }
    public bool CanEdit { get; set; }
    public string? Remark { get; set; }
    public bool HasRecordedStatus { get; set; }
}
