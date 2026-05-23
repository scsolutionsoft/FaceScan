using FaceScan.Web.Models.Enums;

namespace FaceScan.Web.Helpers;

public static class PeriodAttendanceStatusPalette
{
    public static IReadOnlyList<PeriodAttendanceStatus> OrderedStatuses { get; } =
    [
        PeriodAttendanceStatus.Present,
        PeriodAttendanceStatus.Leave,
        PeriodAttendanceStatus.Absent,
        PeriodAttendanceStatus.Truancy,
        PeriodAttendanceStatus.Late,
        PeriodAttendanceStatus.Other
    ];

    public static string GetCssClass(PeriodAttendanceStatus status) => status switch
    {
        PeriodAttendanceStatus.Present => "is-present",
        PeriodAttendanceStatus.Leave => "is-leave",
        PeriodAttendanceStatus.Absent => "is-absent",
        PeriodAttendanceStatus.Truancy => "is-truancy",
        PeriodAttendanceStatus.Late => "is-late",
        PeriodAttendanceStatus.Other => "is-other",
        _ => "is-empty"
    };
}
