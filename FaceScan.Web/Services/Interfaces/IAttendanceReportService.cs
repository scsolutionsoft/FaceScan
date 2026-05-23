using FaceScan.Web.ViewModels.Attendance;

namespace FaceScan.Web.Services.Interfaces;

public interface IAttendanceReportService
{
    Task<AttendanceIndexViewModel> GetTransactionsAsync(AttendanceFilterViewModel filter, CancellationToken cancellationToken = default);
    Task<AttendanceDailyViewModel> GetDailyAsync(AttendanceFilterViewModel filter, CancellationToken cancellationToken = default);
    Task<AttendanceDailyViewModel> GetLateAsync(AttendanceFilterViewModel filter, CancellationToken cancellationToken = default);
    Task<AttendanceByClassViewModel> GetByClassAsync(AttendanceFilterViewModel filter, CancellationToken cancellationToken = default);
    Task<byte[]> ExportDailyCsvAsync(AttendanceFilterViewModel filter, CancellationToken cancellationToken = default);
    Task<byte[]> ExportLateCsvAsync(AttendanceFilterViewModel filter, CancellationToken cancellationToken = default);
    Task<byte[]> ExportByClassCsvAsync(AttendanceFilterViewModel filter, CancellationToken cancellationToken = default);
}
