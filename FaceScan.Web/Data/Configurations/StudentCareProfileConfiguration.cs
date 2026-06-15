using FaceScan.Web.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FaceScan.Web.Data.Configurations;

public class StudentCareProfileConfiguration : IEntityTypeConfiguration<StudentCareProfile>
{
    public void Configure(EntityTypeBuilder<StudentCareProfile> builder)
    {
        builder.ToTable("StudentCareProfiles");
        builder.Property(x => x.LivesWith).HasMaxLength(120);
        builder.Property(x => x.GuardianRelationship).HasMaxLength(120);
        builder.Property(x => x.HouseholdIncome).HasColumnType("decimal(12,2)");
        builder.Property(x => x.IncomeRange).HasMaxLength(80);
        builder.Property(x => x.HomeLatitude).HasColumnType("decimal(9,6)");
        builder.Property(x => x.HomeLongitude).HasColumnType("decimal(9,6)");
        builder.Property(x => x.RiskBehaviors).HasMaxLength(1000);
        builder.Property(x => x.ProblemsFound).HasMaxLength(1000);
        builder.Property(x => x.SupportRecommendation).HasMaxLength(1000);

        builder.HasIndex(x => x.StudentId).IsUnique();
        builder.HasIndex(x => x.RiskLevel);

        builder.HasOne(x => x.Student)
            .WithOne()
            .HasForeignKey<StudentCareProfile>(x => x.StudentId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
