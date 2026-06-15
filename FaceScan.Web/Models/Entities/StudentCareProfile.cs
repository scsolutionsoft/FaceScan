using FaceScan.Web.Models.Enums;

namespace FaceScan.Web.Models.Entities;

public class StudentCareProfile : BaseEntity
{
    public int StudentId { get; set; }
    public string? LivesWith { get; set; }
    public string? GuardianRelationship { get; set; }
    public decimal? HouseholdIncome { get; set; }
    public string? IncomeRange { get; set; }
    public decimal? HomeLatitude { get; set; }
    public decimal? HomeLongitude { get; set; }
    public DateTime? HomeLocationSharedAt { get; set; }
    public StudentCareRiskLevel RiskLevel { get; set; } = StudentCareRiskLevel.Normal;
    public string? RiskBehaviors { get; set; }
    public string? ProblemsFound { get; set; }
    public string? SupportRecommendation { get; set; }
    public DateTime? LastHomeVisitAt { get; set; }

    public Student? Student { get; set; }
}
