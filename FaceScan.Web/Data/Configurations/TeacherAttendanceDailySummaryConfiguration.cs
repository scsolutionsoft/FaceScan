using FaceScan.Web.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FaceScan.Web.Data.Configurations;

public class TeacherAttendanceDailySummaryConfiguration : IEntityTypeConfiguration<TeacherAttendanceDailySummary>
{
    public void Configure(EntityTypeBuilder<TeacherAttendanceDailySummary> builder)
    {
        builder.ToTable("TeacherAttendanceDailySummaries");

        builder.Property(x => x.UserId).HasMaxLength(450).IsRequired();
        builder.Property(x => x.Remark).HasMaxLength(500);

        builder.HasOne(x => x.User)
            .WithMany(x => x.TeacherAttendanceDailySummaries)
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => x.Date);
        builder.HasIndex(x => new { x.UserId, x.Date }).IsUnique();
    }
}
