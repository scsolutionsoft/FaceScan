using System.ComponentModel.DataAnnotations;

namespace FaceScan.Web.Models.Enums;

public enum ScanType
{
    [Display(Name = "เข้าเรียน")]
    CheckIn = 1,
    [Display(Name = "กลับบ้าน")]
    CheckOut = 2
}
