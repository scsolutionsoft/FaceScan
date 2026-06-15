namespace FaceScan.Web.ViewModels.Teacher;

public class StudentCareWasteBankReportRowViewModel
{
    public int StudentId { get; set; }
    public string StudentCode { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Classroom { get; set; } = "-";
    public decimal TotalWeightKg { get; set; }
    public decimal TotalAmount { get; set; }
    public int TransactionCount { get; set; }
}
