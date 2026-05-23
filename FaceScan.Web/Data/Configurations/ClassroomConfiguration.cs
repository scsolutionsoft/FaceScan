using FaceScan.Web.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FaceScan.Web.Data.Configurations;

public class ClassroomConfiguration : IEntityTypeConfiguration<Classroom>
{
    public void Configure(EntityTypeBuilder<Classroom> builder)
    {
        builder.ToTable("Classrooms");
        builder.Property(x => x.Name).HasMaxLength(50).IsRequired();
        builder.Property(x => x.RoomCode).HasMaxLength(30);

        builder.HasOne(x => x.AcademicYear)
            .WithMany(x => x.Classrooms)
            .HasForeignKey(x => x.AcademicYearId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.GradeLevel)
            .WithMany(x => x.Classrooms)
            .HasForeignKey(x => x.GradeLevelId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => new { x.AcademicYearId, x.GradeLevelId, x.Name }).IsUnique();
    }
}
