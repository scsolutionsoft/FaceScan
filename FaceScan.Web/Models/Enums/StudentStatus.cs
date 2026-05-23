using System.ComponentModel.DataAnnotations;

namespace FaceScan.Web.Models.Enums;

public enum StudentStatus
{
    [Display(Name = "กำลังศึกษา")]
    Active = 1,
    [Display(Name = "ย้ายสถานศึกษา")]
    Transferred = 2,
    [Display(Name = "สำเร็จการศึกษา")]
    Graduated = 3,
    [Display(Name = "ไม่มีสถานะใช้งาน")]
    Inactive = 4
}
