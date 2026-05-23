using System.ComponentModel.DataAnnotations;

namespace FaceScan.Web.Models.Enums;

public enum ImportBatchStatus
{
    [Display(Name = "รอดำเนินการ")]
    Pending = 0,
    [Display(Name = "สำเร็จ")]
    Completed = 1,
    [Display(Name = "สำเร็จแต่มีข้อผิดพลาด")]
    CompletedWithErrors = 2,
    [Display(Name = "ล้มเหลว")]
    Failed = 3
}
