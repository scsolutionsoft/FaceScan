using System.ComponentModel.DataAnnotations;

namespace FaceScan.Web.ViewModels.Teacher;

public class WasteBankTransactionInputViewModel
{
    [Required]
    public int StudentId { get; set; }

    [Required]
    [StringLength(160)]
    public string WasteType { get; set; } = string.Empty;

    [Range(0.001, 1000)]
    public decimal WeightKg { get; set; }

    [StringLength(500)]
    public string? Note { get; set; }
}
