using FaceScan.Web.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FaceScan.Web.Data.Configurations;

public class StudentCareFollowUpCaseConfiguration : IEntityTypeConfiguration<StudentCareFollowUpCase>
{
    public void Configure(EntityTypeBuilder<StudentCareFollowUpCase> builder)
    {
        builder.ToTable("StudentCareFollowUpCases");
        builder.Property(x => x.Title).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Concern).HasMaxLength(1000).IsRequired();
        builder.Property(x => x.SupportPlan).HasMaxLength(1000).IsRequired();
        builder.Property(x => x.Outcome).HasMaxLength(1000);
        builder.Property(x => x.AssignedToUserId).HasMaxLength(450);
        builder.Property(x => x.CreatedByUserId).HasMaxLength(450);
        builder.Property(x => x.LastUpdatedByUserId).HasMaxLength(450);

        builder.HasIndex(x => new { x.StudentId, x.AcademicYearId });
        builder.HasIndex(x => x.Status);
        builder.HasIndex(x => x.Priority);
        builder.HasIndex(x => x.DueDate);
        builder.HasIndex(x => x.OpenedAt);

        builder.HasOne(x => x.Student)
            .WithMany()
            .HasForeignKey(x => x.StudentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.AcademicYear)
            .WithMany()
            .HasForeignKey(x => x.AcademicYearId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.AssignedToUser)
            .WithMany()
            .HasForeignKey(x => x.AssignedToUserId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne(x => x.CreatedByUser)
            .WithMany()
            .HasForeignKey(x => x.CreatedByUserId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne(x => x.LastUpdatedByUser)
            .WithMany()
            .HasForeignKey(x => x.LastUpdatedByUserId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}
