using System.ComponentModel.DataAnnotations;

namespace FaceScan.Web.Models.Enums;

public enum GenderType
{
    [Display(Name = "ไม่ระบุ")]
    Unknown = 0,
    [Display(Name = "ชาย")]
    Male = 1,
    [Display(Name = "หญิง")]
    Female = 2,
    [Display(Name = "อื่นๆ")]
    Other = 3
}
