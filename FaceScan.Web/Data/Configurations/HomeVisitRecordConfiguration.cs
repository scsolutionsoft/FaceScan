using FaceScan.Web.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FaceScan.Web.Data.Configurations;

public class HomeVisitRecordConfiguration : IEntityTypeConfiguration<HomeVisitRecord>
{
    public void Configure(EntityTypeBuilder<HomeVisitRecord> builder)
    {
        builder.ToTable("HomeVisitRecords");
        builder.Property(x => x.TeacherUserId).HasMaxLength(450);
        builder.Property(x => x.LivesWith).HasMaxLength(120);
        builder.Property(x => x.HouseCondition).HasMaxLength(500);
        builder.Property(x => x.FamilyRelationship).HasMaxLength(500);
        builder.Property(x => x.LearningSupportAtHome).HasMaxLength(500);
        builder.Property(x => x.HouseholdIncome).HasColumnType("decimal(12,2)");
        builder.Property(x => x.RiskBehaviors).HasMaxLength(1000);
        builder.Property(x => x.ProblemsFound).HasMaxLength(1000);
        builder.Property(x => x.TeacherObservation).HasMaxLength(1000);
        builder.Property(x => x.SupportPlan).HasMaxLength(1000);
        builder.Property(x => x.ParentPhotoPath).HasMaxLength(300);
        builder.Property(x => x.HousePhotoPath).HasMaxLength(300);
        builder.Property(x => x.Latitude).HasColumnType("decimal(9,6)");
        builder.Property(x => x.Longitude).HasColumnType("decimal(9,6)");
        builder.Property(x => x.ApprovedByUserId).HasMaxLength(450);

        builder.HasIndex(x => new { x.StudentId, x.AcademicYearId });
        builder.HasIndex(x => x.VisitDate);
        builder.HasIndex(x => x.VisitStatus);

        builder.HasOne(x => x.Student)
            .WithMany()
            .HasForeignKey(x => x.StudentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.AcademicYear)
            .WithMany()
            .HasForeignKey(x => x.AcademicYearId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.TeacherUser)
            .WithMany()
            .HasForeignKey(x => x.TeacherUserId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne(x => x.ApprovedByUser)
            .WithMany()
            .HasForeignKey(x => x.ApprovedByUserId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}
