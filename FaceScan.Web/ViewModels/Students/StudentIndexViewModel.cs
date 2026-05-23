using FaceScan.Web.ViewModels;

namespace FaceScan.Web.ViewModels.Students;

public class StudentIndexViewModel
{
    public StudentFilterViewModel Filter { get; set; } = new();
    public IReadOnlyList<StudentListItemViewModel> Students { get; set; } = [];
    public IReadOnlyList<SelectOptionViewModel> AcademicYears { get; set; } = [];
    public IReadOnlyList<SelectOptionViewModel> GradeLevels { get; set; } = [];
    public IReadOnlyList<SelectOptionViewModel> Classrooms { get; set; } = [];
    public int TotalCount { get; set; }
}
