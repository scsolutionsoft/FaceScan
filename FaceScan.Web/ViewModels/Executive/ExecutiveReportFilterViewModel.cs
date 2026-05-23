namespace FaceScan.Web.ViewModels.Executive;

public class ExecutiveReportFilterViewModel
{
    public DateTime DateFrom { get; set; } = DateTime.Today;
    public DateTime DateTo { get; set; } = DateTime.Today;
    public int? GradeLevelId { get; set; }
    public int? ClassroomId { get; set; }
    public string? StudentKeyword { get; set; }
}
