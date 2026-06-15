using System.ComponentModel.DataAnnotations;
using FaceScan.Web.Models.Enums;
using Microsoft.AspNetCore.Http;

namespace FaceScan.Web.ViewModels.Teacher;

public class HomeVisitFormViewModel
{
    public int? Id { get; set; }

    [Required]
    public int StudentId { get; set; }

    public string StudentCode { get; set; } = string.Empty;
    public string StudentName { get; set; } = string.Empty;
    public string Classroom { get; set; } = "-";

    [Required]
    public DateTime VisitDate { get; set; } = DateTime.Today;

    public HomeVisitStatus VisitStatus { get; set; } = HomeVisitStatus.Completed;

    [StringLength(120)]
    public string? LivesWith { get; set; }

    [StringLength(120)]
    public string? GuardianName { get; set; }

    [StringLength(20)]
    public string? GuardianNationalId { get; set; }

    [StringLength(120)]
    public string? GuardianRelationship { get; set; }

    [StringLength(40)]
    public string? GuardianPhone { get; set; }

    [StringLength(160)]
    public string? GuardianOccupation { get; set; }

    public decimal? HouseholdIncome { get; set; }

    [StringLength(500)]
    public string? HouseCondition { get; set; }

    [StringLength(500)]
    public string? FamilyRelationship { get; set; }

    [StringLength(500)]
    public string? LearningSupportAtHome { get; set; }

    [StringLength(1000)]
    public string? RiskBehaviors { get; set; }

    [StringLength(1000)]
    public string? ProblemsFound { get; set; }

    [StringLength(1000)]
    public string? TeacherObservation { get; set; }

    [StringLength(1000)]
    public string? SupportPlan { get; set; }

    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }
    public StudentCareRiskLevel RiskLevel { get; set; } = StudentCareRiskLevel.Normal;

    public string? CurrentParentPhotoPath { get; set; }
    public string? CurrentHousePhotoPath { get; set; }
    public IFormFile? ParentPhoto { get; set; }
    public IFormFile? HousePhoto { get; set; }
}
