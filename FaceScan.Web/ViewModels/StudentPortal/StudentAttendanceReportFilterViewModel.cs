using System.ComponentModel.DataAnnotations;

namespace FaceScan.Web.ViewModels.StudentPortal;

public class StudentAttendanceReportFilterViewModel
{
    [DataType(DataType.Date)]
    public DateTime? StartDate { get; set; }

    [DataType(DataType.Date)]
    public DateTime? EndDate { get; set; }
}
