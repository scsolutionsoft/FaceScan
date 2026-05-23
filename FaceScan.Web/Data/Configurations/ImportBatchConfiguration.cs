using FaceScan.Web.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FaceScan.Web.Data.Configurations;

public class ImportBatchConfiguration : IEntityTypeConfiguration<ImportBatch>
{
    public void Configure(EntityTypeBuilder<ImportBatch> builder)
    {
        builder.ToTable("ImportBatches");
        builder.Property(x => x.FileName).HasMaxLength(255).IsRequired();

        builder.HasOne(x => x.ImportedByUser)
            .WithMany()
            .HasForeignKey(x => x.ImportedByUserId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
