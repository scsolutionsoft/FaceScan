using System.ComponentModel.DataAnnotations;

namespace FaceScan.Web.Models.Enums;

public enum EnrollmentStatus
{
    [Display(Name = "ยังไม่ลงทะเบียน")]
    NotRegistered = 0,
    [Display(Name = "รอตรวจสอบ")]
    Pending = 1,
    [Display(Name = "พร้อมใช้งาน")]
    Ready = 2,
    [Display(Name = "ไม่ผ่าน")]
    Rejected = 3
}
