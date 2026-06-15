namespace FaceScan.Web.Models.Entities;

public class WasteBankRate : BaseEntity
{
    public string WasteType { get; set; } = string.Empty;
    public decimal PricePerKg { get; set; }
    public DateTime EffectiveFrom { get; set; } = DateTime.Today;
    public DateTime? EffectiveTo { get; set; }
    public bool IsActive { get; set; } = true;
}
