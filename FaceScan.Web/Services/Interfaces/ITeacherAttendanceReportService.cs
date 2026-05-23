using FaceScan.Web.ViewModels.TeacherAttendance;

namespace FaceScan.Web.Services.Interfaces;

public interface ITeacherAttendanceReportService
{
    Task<TeacherAttendanceIndexViewModel> GetTransactionsAsync(TeacherAttendanceFilterViewModel filter, CancellationToken cancellationToken = default);
    Task<TeacherAttendanceDailyViewModel> GetDailyAsync(TeacherAttendanceFilterViewModel filter, CancellationToken cancellationToken = default);
    Task<TeacherAttendanceDailyViewModel> GetLateAsync(TeacherAttendanceFilterViewModel filter, CancellationToken cancellationToken = default);
}
