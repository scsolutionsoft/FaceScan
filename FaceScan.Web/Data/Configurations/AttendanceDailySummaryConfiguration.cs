using FaceScan.Web.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FaceScan.Web.Data.Configurations;

public class AttendanceDailySummaryConfiguration : IEntityTypeConfiguration<AttendanceDailySummary>
{
    public void Configure(EntityTypeBuilder<AttendanceDailySummary> builder)
    {
        builder.ToTable("AttendanceDailySummaries");
        builder.Property(x => x.Remark).HasMaxLength(500);

        builder.HasOne(x => x.Student)
            .WithMany(x => x.AttendanceDailySummaries)
            .HasForeignKey(x => x.StudentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Classroom)
            .WithMany()
            .HasForeignKey(x => x.ClassroomId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(x => x.Date);
        builder.HasIndex(x => new { x.ClassroomId, x.Date });
        builder.HasIndex(x => new { x.StudentId, x.Date }).IsUnique();
    }
}
