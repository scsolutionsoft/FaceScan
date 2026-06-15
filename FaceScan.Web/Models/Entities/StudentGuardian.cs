namespace FaceScan.Web.Models.Entities;

public class StudentGuardian : BaseEntity
{
    public int StudentId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string? NationalId { get; set; }
    public string? Relationship { get; set; }
    public string? PhoneNumber { get; set; }
    public string? Occupation { get; set; }
    public decimal? MonthlyIncome { get; set; }
    public string? Address { get; set; }
    public string? PhotoPath { get; set; }
    public bool IsPrimaryContact { get; set; }

    public Student? Student { get; set; }
}
