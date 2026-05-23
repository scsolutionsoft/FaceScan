using FaceScan.Web.ViewModels.Dashboard;

namespace FaceScan.Web.Services.Interfaces;

public interface IDashboardService
{
    Task<DashboardViewModel> GetDashboardAsync(DateTime? date = null, CancellationToken cancellationToken = default);
}
