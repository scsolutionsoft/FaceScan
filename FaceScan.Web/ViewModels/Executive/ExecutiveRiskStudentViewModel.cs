namespace FaceScan.Web.ViewModels.Executive;

public class ExecutiveRiskStudentViewModel
{
    public int StudentId { get; set; }
    public string StudentCode { get; set; } = string.Empty;
    public string StudentName { get; set; } = string.Empty;
    public string GradeLevel { get; set; } = string.Empty;
    public string Classroom { get; set; } = string.Empty;
    public decimal PresentRate { get; set; }
    public int AbsentPeriods { get; set; }
    public int TruancyPeriods { get; set; }
    public int LatePeriods { get; set; }
    public int TotalTrackedPeriods { get; set; }
    public decimal RiskScore { get; set; }
    public string RiskLevel { get; set; } = string.Empty;
    public string RiskReason { get; set; } = string.Empty;
}