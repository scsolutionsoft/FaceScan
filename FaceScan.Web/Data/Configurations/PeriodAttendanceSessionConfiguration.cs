using FaceScan.Web.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FaceScan.Web.Data.Configurations;

public class PeriodAttendanceSessionConfiguration : IEntityTypeConfiguration<PeriodAttendanceSession>
{
    public void Configure(EntityTypeBuilder<PeriodAttendanceSession> builder)
    {
        builder.ToTable("PeriodAttendanceSessions");

        builder.Property(x => x.TeacherStatusNote).HasMaxLength(500);
        builder.Property(x => x.CheckedByUserId).HasMaxLength(450).IsRequired();

        builder.HasOne(x => x.Classroom)
            .WithMany(x => x.PeriodAttendanceSessions)
            .HasForeignKey(x => x.ClassroomId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.ClassPeriod)
            .WithMany(x => x.PeriodAttendanceSessions)
            .HasForeignKey(x => x.ClassPeriodId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.CheckedByUser)
            .WithMany(x => x.PeriodAttendanceSessions)
            .HasForeignKey(x => x.CheckedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.Date);
        builder.HasIndex(x => new { x.Date, x.ClassroomId, x.ClassPeriodId }).IsUnique();
    }
}
