using FaceScan.Web.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FaceScan.Web.Data.Configurations;

public class ImportBatchItemConfiguration : IEntityTypeConfiguration<ImportBatchItem>
{
    public void Configure(EntityTypeBuilder<ImportBatchItem> builder)
    {
        builder.ToTable("ImportBatchItems");
        builder.Property(x => x.StudentCode).HasMaxLength(50);
        builder.Property(x => x.ErrorMessage).HasMaxLength(500);

        builder.HasOne(x => x.ImportBatch)
            .WithMany(x => x.Items)
            .HasForeignKey(x => x.ImportBatchId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
