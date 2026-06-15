namespace FaceScan.Web.ViewModels.Teacher;

public class StudentCareStudentOptionViewModel
{
    public int Value { get; set; }
    public string Text { get; set; } = string.Empty;
    public string StudentCode { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string? NationalId { get; set; }
    public string? GuardianNationalId { get; set; }
    public int GradeLevelId { get; set; }
    public int ClassroomId { get; set; }
    public string? PhotoPath { get; set; }
}
