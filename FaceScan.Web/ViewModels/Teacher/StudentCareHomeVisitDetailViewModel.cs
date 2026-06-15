using FaceScan.Web.Models.Enums;

namespace FaceScan.Web.ViewModels.Teacher;

public class StudentCareHomeVisitDetailViewModel
{
    public DateTime VisitDate { get; set; }
    public HomeVisitStatus VisitStatus { get; set; }
    public string? LivesWith { get; set; }
    public decimal? HouseholdIncome { get; set; }
    public string? HouseCondition { get; set; }
    public string? FamilyRelationship { get; set; }
    public string? LearningSupportAtHome { get; set; }
    public string? RiskBehaviors { get; set; }
    public string? ProblemsFound { get; set; }
    public string? TeacherObservation { get; set; }
    public string? SupportPlan { get; set; }
    public string? ParentPhotoPath { get; set; }
    public string? HousePhotoPath { get; set; }
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }
}
