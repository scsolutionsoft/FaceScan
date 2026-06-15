using System.ComponentModel.DataAnnotations;
using FaceScan.Web.Models.Enums;

namespace FaceScan.Web.ViewModels.Teacher;

public class StudentCareFollowUpCaseInputViewModel
{
    [Required]
    public int StudentId { get; set; }

    [Required]
    [StringLength(200)]
    public string Title { get; set; } = string.Empty;

    [Required]
    [StringLength(1000)]
    public string Concern { get; set; } = string.Empty;

    [Required]
    [StringLength(1000)]
    public string SupportPlan { get; set; } = string.Empty;

    public StudentCareFollowUpPriority Priority { get; set; } = StudentCareFollowUpPriority.Normal;
    public DateTime? DueDate { get; set; }
}
