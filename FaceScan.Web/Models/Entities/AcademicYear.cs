namespace FaceScan.Web.Models.Entities;

public class AcademicYear : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public bool IsCurrent { get; set; }
    public bool IsActive { get; set; } = true;

    public ICollection<Classroom> Classrooms { get; set; } = new List<Classroom>();
    public ICollection<Student> Students { get; set; } = new List<Student>();
}
