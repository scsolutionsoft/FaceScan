using System.ComponentModel.DataAnnotations;

namespace FaceScan.Web.ViewModels.Teacher;

public class WasteBankRateInputViewModel
{
    [Required]
    [StringLength(160)]
    public string WasteType { get; set; } = string.Empty;

    [Range(0.01, 1000)]
    public decimal PricePerKg { get; set; }
}
