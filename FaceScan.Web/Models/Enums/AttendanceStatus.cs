using System.ComponentModel.DataAnnotations;

namespace FaceScan.Web.Models.Enums;

public enum AttendanceStatus
{
    [Display(Name = "ขาด")]
    Absent = 0,
    [Display(Name = "มาเรียน")]
    Present = 1,
    [Display(Name = "ข้อมูลไม่สมบูรณ์")]
    Partial = 2,
    [Display(Name = "ยังไม่เช็คกลับ")]
    PendingCheckout = 3
}
