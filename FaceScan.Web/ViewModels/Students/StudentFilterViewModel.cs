namespace FaceScan.Web.ViewModels.Students;

public class StudentFilterViewModel
{
    public string? SearchText { get; set; }
    public int? AcademicYearId { get; set; }
    public int? GradeLevelId { get; set; }
    public int? ClassroomId { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}
