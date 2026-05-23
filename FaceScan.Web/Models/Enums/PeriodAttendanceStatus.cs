using System.ComponentModel.DataAnnotations;

namespace FaceScan.Web.Models.Enums;

public enum PeriodAttendanceStatus
{
    [Display(Name = "มาเรียน")]
    Present = 0,
    [Display(Name = "ขาด")]
    Absent = 1,
    [Display(Name = "ลา")]
    Leave = 2,
    [Display(Name = "มาสาย")]
    Late = 3,
    [Display(Name = "หนีเรียน")]
    Truancy = 4,
    [Display(Name = "อื่นๆ")]
    Other = 5
}
