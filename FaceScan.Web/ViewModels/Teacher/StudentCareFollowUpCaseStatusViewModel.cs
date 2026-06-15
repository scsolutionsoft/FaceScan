using System.ComponentModel.DataAnnotations;
using FaceScan.Web.Models.Enums;

namespace FaceScan.Web.ViewModels.Teacher;

public class StudentCareFollowUpCaseStatusViewModel
{
    [Required]
    public int Id { get; set; }

    [Required]
    public int StudentId { get; set; }

    public StudentCareFollowUpStatus Status { get; set; } = StudentCareFollowUpStatus.InProgress;

    [StringLength(1000)]
    public string? Outcome { get; set; }
}
