namespace FaceScan.Web.ViewModels.Teacher;

public class StudentCareGoodnessReportRowViewModel
{
    public int StudentId { get; set; }
    public string StudentCode { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Classroom { get; set; } = "-";
    public int GoodnessPoint { get; set; }
    public int TransactionCount { get; set; }
}
