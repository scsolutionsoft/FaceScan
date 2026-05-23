using FaceScan.Web.Models.Entities;
using FaceScan.Web.Models.Enums;
using FaceScan.Web.ViewModels.Scan;

namespace FaceScan.Web.Services.Interfaces;

public interface ITeacherAttendanceService
{
    Task<TeacherScanProcessResult> ProcessScanAsync(ScanVerifyRequestViewModel request, Stream imageStream, CancellationToken cancellationToken = default);
    Task<ScanType> DetermineCheckTypeAsync(string userId, DateTime scanTime, ScanType? requestedType = null, CancellationToken cancellationToken = default);
    Task BuildDailySummaryAsync(DateTime date, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TeacherAttendanceDailySummary>> GetTodaySummaryAsync(DateTime date, CancellationToken cancellationToken = default);
}
