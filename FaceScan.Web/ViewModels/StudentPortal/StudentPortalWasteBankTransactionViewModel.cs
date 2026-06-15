namespace FaceScan.Web.ViewModels.StudentPortal;

public class StudentPortalWasteBankTransactionViewModel
{
    public DateTime RecordedAt { get; set; }
    public string WasteType { get; set; } = string.Empty;
    public decimal WeightKg { get; set; }
    public decimal PricePerKg { get; set; }
    public decimal Amount { get; set; }
    public string? Note { get; set; }
}
