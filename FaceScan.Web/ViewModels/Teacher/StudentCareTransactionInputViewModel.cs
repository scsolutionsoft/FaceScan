using System.ComponentModel.DataAnnotations;

namespace FaceScan.Web.ViewModels.Teacher;

public class StudentCareTransactionInputViewModel
{
    [Required]
    public int StudentId { get; set; }

    public int? CategoryId { get; set; }

    [StringLength(120)]
    public string Category { get; set; } = string.Empty;

    [Range(-100, 100)]
    public int ScoreChange { get; set; }

    [Range(1, 100)]
    public int Point { get; set; } = 1;

    [StringLength(1000)]
    public string? Description { get; set; }
}
