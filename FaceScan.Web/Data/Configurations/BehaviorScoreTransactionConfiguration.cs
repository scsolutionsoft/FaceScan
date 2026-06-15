using FaceScan.Web.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FaceScan.Web.Data.Configurations;

public class BehaviorScoreTransactionConfiguration : IEntityTypeConfiguration<BehaviorScoreTransaction>
{
    public void Configure(EntityTypeBuilder<BehaviorScoreTransaction> builder)
    {
        builder.ToTable("BehaviorScoreTransactions");
        builder.Property(x => x.Category).HasMaxLength(120).IsRequired();
        builder.Property(x => x.Reason).HasMaxLength(1000).IsRequired();
        builder.Property(x => x.EvidencePhotoPath).HasMaxLength(300);
        builder.Property(x => x.RecordedByUserId).HasMaxLength(450);
        builder.Property(x => x.ApprovedByUserId).HasMaxLength(450);

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

        builder.HasOne(x => x.ApprovedByUser)
            .WithMany()
            .HasForeignKey(x => x.ApprovedByUserId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}
