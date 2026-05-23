using System.ComponentModel.DataAnnotations;

namespace FaceScan.Web.Models.Enums;

public enum TeacherTeachingStatus
{
    [Display(Name = "ปกติ")]
    Normal = 0,
    [Display(Name = "ไม่ปกติ")]
    Abnormal = 1
}
