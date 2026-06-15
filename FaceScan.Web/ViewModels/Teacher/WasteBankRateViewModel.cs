namespace FaceScan.Web.ViewModels.Teacher;

public class WasteBankRateViewModel
{
    public string WasteType { get; set; } = string.Empty;
    public decimal PricePerKg { get; set; }
    public DateTime EffectiveFrom { get; set; }
}
