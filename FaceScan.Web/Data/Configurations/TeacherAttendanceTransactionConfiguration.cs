using FaceScan.Web.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FaceScan.Web.Data.Configurations;

public class TeacherAttendanceTransactionConfiguration : IEntityTypeConfiguration<TeacherAttendanceTransaction>
{
    public void Configure(EntityTypeBuilder<TeacherAttendanceTransaction> builder)
    {
        builder.ToTable("TeacherAttendanceTransactions");

        builder.Property(x => x.UserId).HasMaxLength(450).IsRequired();
        builder.Property(x => x.ConfidenceScore).HasColumnType("decimal(5,4)");
        builder.Property(x => x.Latitude).HasColumnType("decimal(9,6)");
        builder.Property(x => x.Longitude).HasColumnType("decimal(9,6)");
        builder.Property(x => x.LocationAccuracyMeters).HasColumnType("decimal(8,2)");
        builder.Property(x => x.RecognitionProvider).HasMaxLength(100).IsRequired();
        builder.Property(x => x.SnapshotPath).HasMaxLength(350);
        builder.Property(x => x.RawResponseJson).HasMaxLength(4000);

        builder.HasOne(x => x.User)
            .WithMany(x => x.TeacherAttendanceTransactions)
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Device)
            .WithMany()
            .HasForeignKey(x => x.DeviceId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(x => x.ScanDate);
        builder.HasIndex(x => new { x.UserId, x.ScanDate });
    }
}
