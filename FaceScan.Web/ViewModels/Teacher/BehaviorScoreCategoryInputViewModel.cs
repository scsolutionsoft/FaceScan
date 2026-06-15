using System.ComponentModel.DataAnnotations;

namespace FaceScan.Web.ViewModels.Teacher;

public class BehaviorScoreCategoryInputViewModel
{
    public int? Id { get; set; }

    [Required]
    [StringLength(120)]
    public string Name { get; set; } = string.Empty;

    [Range(-500, 500)]
    public int ScoreChange { get; set; }

    [StringLength(500)]
    public string? Description { get; set; }

    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
}
