using FaceScan.Web.ViewModels;

namespace FaceScan.Web.ViewModels.Teacher;

public class WasteBankDashboardViewModel
{
    public WasteBankFilterViewModel Filter { get; set; } = new();
    public WasteBankRateInputViewModel WasteRateInput { get; set; } = new();
    public WasteBankTransactionInputViewModel WasteTransactionInput { get; set; } = new();
    public List<SelectOptionViewModel> GradeLevelOptions { get; set; } = [];
    public List<SelectOptionViewModel> ClassroomOptions { get; set; } = [];
    public List<SelectOptionViewModel> StudentOptions { get; set; } = [];
    public List<WasteBankRateViewModel> ActiveWasteRates { get; set; } = [];
    public List<StudentCareWasteBankReportRowViewModel> Students { get; set; } = [];
    public decimal TotalWeightKg { get; set; }
    public decimal TotalAmount { get; set; }
}
