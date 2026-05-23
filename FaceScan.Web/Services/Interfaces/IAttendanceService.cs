using FaceScan.Web.Models.Entities;
using FaceScan.Web.Models.Enums;
using FaceScan.Web.ViewModels.Attendance;
using FaceScan.Web.ViewModels.Scan;

namespace FaceScan.Web.Services.Interfaces;

public interface IAttendanceService
{
    Task<ScanProcessResult> ProcessScanAsync(ScanVerifyRequestViewModel request, Stream imageStream, CancellationToken cancellationToken = default);
    Task<ScanType> DetermineCheckTypeAsync(int studentId, DateTime scanTime, ScanType? requestedType = null, CancellationToken cancellationToken = default);
    Task<bool> IsDuplicateScanAsync(int studentId, DateTime scanTime, CancellationToken cancellationToken = default);
    Task BuildDailySummaryAsync(DateTime date, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AttendanceDailySummary>> GetTodaySummaryAsync(AttendanceFilterViewModel filter, CancellationToken cancellationToken = default);
}
