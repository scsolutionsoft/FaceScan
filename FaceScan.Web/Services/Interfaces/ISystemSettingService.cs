using FaceScan.Web.Models.Entities;
using FaceScan.Web.ViewModels.Settings;

namespace FaceScan.Web.Services.Interfaces;

public interface ISystemSettingService
{
    Task<SystemSetting> GetSettingsAsync(CancellationToken cancellationToken = default);
    Task<SystemSettingsViewModel> GetSettingsViewModelAsync(CancellationToken cancellationToken = default);
    Task<(bool Success, string Message)> UpdateBrandingAsync(BrandingSettingsViewModel model, string? userId, string? ipAddress, CancellationToken cancellationToken = default);
    Task<(bool Success, string Message)> UpdateGeneralAsync(GeneralSettingsViewModel model, string? userId, string? ipAddress, CancellationToken cancellationToken = default);
    Task<(bool Success, string Message)> UpdatePeriodsAsync(PeriodSettingsViewModel model, string? userId, string? ipAddress, CancellationToken cancellationToken = default);
}
