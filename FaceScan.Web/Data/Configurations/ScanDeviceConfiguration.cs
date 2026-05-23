using FaceScan.Web.Models.Entities;
using FaceScan.Web.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FaceScan.Web.Data.Configurations;

public class ScanDeviceConfiguration : IEntityTypeConfiguration<ScanDevice>
{
    public void Configure(EntityTypeBuilder<ScanDevice> builder)
    {
        builder.ToTable("ScanDevices");
        builder.Property(x => x.Name).HasMaxLength(100).IsRequired();
        builder.Property(x => x.StationCode).HasMaxLength(50).IsRequired();
        builder.Property(x => x.AccessToken).HasMaxLength(120).IsRequired();
        builder.Property(x => x.Location).HasMaxLength(200);
        builder.Property(x => x.Notes).HasMaxLength(500);
        builder.Property(x => x.RecognitionProfile).HasMaxLength(20).IsRequired().HasDefaultValue(FaceRecognitionProfiles.Auto);
        builder.Property(x => x.CameraMinConfidence).HasPrecision(18, 2);

        // RTSP configuration properties
        builder.Property(x => x.RtspStreamUrl).HasMaxLength(500);
        builder.Property(x => x.RtspPort).HasDefaultValue(554);
        builder.Property(x => x.RtspUsername).HasMaxLength(100);
        builder.Property(x => x.RtspPassword).HasMaxLength(100);

        // ONVIF configuration properties
        builder.Property(x => x.OnvifDeviceUri).HasMaxLength(500);
        builder.Property(x => x.OnvifPort).HasDefaultValue(8080);
        builder.Property(x => x.OnvifManufacturer).HasMaxLength(100);
        builder.Property(x => x.OnvifModel).HasMaxLength(100);
        builder.Property(x => x.OnvifSerialNumber).HasMaxLength(100);
        builder.Property(x => x.OnvifFirmwareVersion).HasMaxLength(100);

        builder.HasIndex(x => x.StationCode).IsUnique();
    }
}
