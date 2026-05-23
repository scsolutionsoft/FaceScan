using FaceScan.Web.Models.Enums;

namespace FaceScan.Web.Models.Entities;

public class ImportBatch : BaseEntity
{
    public string FileName { get; set; } = string.Empty;
    public DateTime ImportedAt { get; set; } = DateTime.UtcNow;
    public string? ImportedByUserId { get; set; }
    public int TotalRows { get; set; }
    public int SuccessRows { get; set; }
    public int FailedRows { get; set; }
    public ImportBatchStatus Status { get; set; } = ImportBatchStatus.Pending;

    public ApplicationUser? ImportedByUser { get; set; }
    public ICollection<ImportBatchItem> Items { get; set; } = new List<ImportBatchItem>();
}
