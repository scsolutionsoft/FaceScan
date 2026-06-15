namespace FaceScan.Web.ViewModels.Executive;

public class ExecutiveStudentCareGroupSummaryViewModel
{
    public string Classroom { get; set; } = "-";
    public int StudentCount { get; set; }
    public int HomeVisitedCount { get; set; }
    public int RiskStudentCount { get; set; }
    public decimal WasteWeightKg { get; set; }
    public decimal WasteAmount { get; set; }
}
