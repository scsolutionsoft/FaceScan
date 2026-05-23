namespace FaceScan.Web.Models.Entities;

public class GradeLevel : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;

    public ICollection<Classroom> Classrooms { get; set; } = new List<Classroom>();
    public ICollection<Student> Students { get; set; } = new List<Student>();
}
