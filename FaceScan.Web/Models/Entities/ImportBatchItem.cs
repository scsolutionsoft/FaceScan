using FaceScan.Web.Models.Enums;

namespace FaceScan.Web.Models.Entities;

public class ImportBatchItem : BaseEntity
{
    public int ImportBatchId { get; set; }
    public int RowNumber { get; set; }
    public string? StudentCode { get; set; }
    public ImportRowResultStatus ResultStatus { get; set; }
    public string? ErrorMessage { get; set; }

    public ImportBatch? ImportBatch { get; set; }
}
