using FaceScan.Web.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FaceScan.Web.Data.Configurations;

public class GoodnessBankTransactionConfiguration : IEntityTypeConfiguration<GoodnessBankTransaction>
{
    public void Configure(EntityTypeBuilder<GoodnessBankTransaction> builder)
    {
        builder.ToTable("GoodnessBankTransactions");
        builder.Property(x => x.GoodnessType).HasMaxLength(160).IsRequired();
        builder.Property(x => x.Description).HasMaxLength(1000);
        builder.Property(x => x.EvidencePhotoPath).HasMaxLength(300);
        builder.Property(x => x.RecordedByUserId).HasMaxLength(450);

        builder.HasIndex(x => new { x.StudentId, x.AcademicYearId });
        builder.HasIndex(x => x.RecordedAt);
        builder.HasIndex(x => x.Status);

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
