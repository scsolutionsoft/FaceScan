using FaceScan.Web.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FaceScan.Web.Data.Configurations;

public class WasteBankTransactionConfiguration : IEntityTypeConfiguration<WasteBankTransaction>
{
    public void Configure(EntityTypeBuilder<WasteBankTransaction> builder)
    {
        builder.ToTable("WasteBankTransactions");
        builder.Property(x => x.WasteType).HasMaxLength(160).IsRequired();
        builder.Property(x => x.WeightKg).HasColumnType("decimal(10,3)");
        builder.Property(x => x.PricePerKg).HasColumnType("decimal(10,2)");
        builder.Property(x => x.Amount).HasColumnType("decimal(12,2)");
        builder.Property(x => x.RecordedByUserId).HasMaxLength(450);
        builder.Property(x => x.Note).HasMaxLength(500);

        builder.HasIndex(x => new { x.StudentId, x.AcademicYearId });
        builder.HasIndex(x => x.RecordedAt);

        builder.HasOne(x => x.Student)
            .WithMany()
            .HasForeignKey(x => x.StudentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.AcademicYear)
            .WithMany()
            .HasForeignKey(x => x.AcademicYearId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.RecordedByUser)
            .WithMany()
            .HasForeignKey(x => x.RecordedByUserId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}
