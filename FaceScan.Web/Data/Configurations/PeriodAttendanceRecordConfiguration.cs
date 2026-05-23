using FaceScan.Web.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FaceScan.Web.Data.Configurations;

public class PeriodAttendanceRecordConfiguration : IEntityTypeConfiguration<PeriodAttendanceRecord>
{
    public void Configure(EntityTypeBuilder<PeriodAttendanceRecord> builder)
    {
        builder.ToTable("PeriodAttendanceRecords");

        builder.Property(x => x.Remark).HasMaxLength(500);

        builder.HasOne(x => x.PeriodAttendanceSession)
            .WithMany(x => x.Records)
            .HasForeignKey(x => x.PeriodAttendanceSessionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Student)
            .WithMany(x => x.PeriodAttendanceRecords)
            .HasForeignKey(x => x.StudentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.StudentId);
        builder.HasIndex(x => new { x.PeriodAttendanceSessionId, x.StudentId }).IsUnique();
    }
}
