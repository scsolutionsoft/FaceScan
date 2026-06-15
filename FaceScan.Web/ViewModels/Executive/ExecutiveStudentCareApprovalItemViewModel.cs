namespace FaceScan.Web.ViewModels.Executive;

public class ExecutiveStudentCareApprovalItemViewModel
{
    public string ItemType { get; set; } = string.Empty;
    public int Id { get; set; }
    public string StudentCode { get; set; } = string.Empty;
    public string StudentName { get; set; } = string.Empty;
    public string Classroom { get; set; } = "-";
    public string Title { get; set; } = string.Empty;
    public string Detail { get; set; } = string.Empty;
    public DateTime SubmittedAt { get; set; }
}
