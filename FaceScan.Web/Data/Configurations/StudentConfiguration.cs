using FaceScan.Web.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FaceScan.Web.Data.Configurations;

public class StudentConfiguration : IEntityTypeConfiguration<Student>
{
    public void Configure(EntityTypeBuilder<Student> builder)
    {
        builder.ToTable("Students");

        builder.Property(x => x.StudentCode).HasMaxLength(50).IsRequired();
        builder.Property(x => x.NationalId).HasMaxLength(20);
        builder.Property(x => x.Prefix).HasMaxLength(20).IsRequired();
        builder.Property(x => x.FirstName).HasMaxLength(100).IsRequired();
        builder.Property(x => x.LastName).HasMaxLength(100).IsRequired();
        builder.Property(x => x.FirstNameEn).HasMaxLength(100);
        builder.Property(x => x.LastNameEn).HasMaxLength(100);
        builder.Property(x => x.RoomNumber).HasMaxLength(20);
        builder.Property(x => x.StudentNo).HasMaxLength(20);
        builder.Property(x => x.GuardianName).HasMaxLength(200);
        builder.Property(x => x.GuardianNationalId).HasMaxLength(20);
        builder.Property(x => x.GuardianPhone).HasMaxLength(20);
        builder.Property(x => x.Address).HasMaxLength(500);
        builder.Property(x => x.Notes).HasMaxLength(1000);

        builder.HasOne(x => x.AcademicYear)
            .WithMany(x => x.Students)
            .HasForeignKey(x => x.AcademicYearId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.GradeLevel)
            .WithMany(x => x.Students)
            .HasForeignKey(x => x.GradeLevelId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Classroom)
            .WithMany(x => x.Students)
            .HasForeignKey(x => x.ClassroomId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.StudentCode).IsUnique();
        builder.HasIndex(x => x.ClassroomId);
        builder.HasIndex(x => x.NationalId);
        builder.HasIndex(x => x.GuardianNationalId);
        builder.HasIndex(x => new { x.AcademicYearId, x.GradeLevelId, x.ClassroomId });
    }
}
