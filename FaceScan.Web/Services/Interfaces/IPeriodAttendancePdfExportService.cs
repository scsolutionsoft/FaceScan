using FaceScan.Web.ViewModels.Teacher;

namespace FaceScan.Web.Services.Interfaces;

public interface IPeriodAttendancePdfExportService
{
    byte[] Generate(PeriodAttendancePageViewModel model, string classroomName, string currentPeriod);
}
