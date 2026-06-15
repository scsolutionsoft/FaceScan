using FaceScan.Web.Models.Enums;

namespace FaceScan.Web.Models.Entities;

public class HomeVisitRecord : BaseEntity
{
    public int StudentId { get; set; }
    public int AcademicYearId { get; set; }
    public DateTime VisitDate { get; set; }
    public string? TeacherUserId { get; set; }
    public HomeVisitStatus VisitStatus { get; set; } = HomeVisitStatus.Planned;
    public string? LivesWith { get; set; }
    public string? HouseCondition { get; set; }
    public string? FamilyRelationship { get; set; }
    public string? LearningSupportAtHome { get; set; }
    public decimal? HouseholdIncome { get; set; }
    public string? RiskBehaviors { get; set; }
    public string? ProblemsFound { get; set; }
    public string? TeacherObservation { get; set; }
    public string? SupportPlan { get; set; }
    public string? ParentPhotoPath { get; set; }
    public string? HousePhotoPath { get; set; }
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }
    public DateTime? SubmittedAt { get; set; }
    public string? ApprovedByUserId { get; set; }
    public DateTime? ApprovedAt { get; set; }

    public Student? Student { get; set; }
    public AcademicYear? AcademicYear { get; set; }
    public ApplicationUser? TeacherUser { get; set; }
    public ApplicationUser? ApprovedByUser { get; set; }
}
