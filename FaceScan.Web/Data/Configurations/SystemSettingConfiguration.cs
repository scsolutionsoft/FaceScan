using FaceScan.Web.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FaceScan.Web.Data.Configurations;

public class SystemSettingConfiguration : IEntityTypeConfiguration<SystemSetting>
{
    public void Configure(EntityTypeBuilder<SystemSetting> builder)
    {
        builder.ToTable("SystemSettings");
        builder.Property(x => x.FaceConfidenceThreshold).HasColumnType("decimal(5,2)");
        builder.Property(x => x.LateAfterTime).HasDefaultValue(new TimeSpan(8, 0, 0));
        builder.Property(x => x.CheckOutStartTime).HasDefaultValue(new TimeSpan(15, 30, 0));
        builder.Property(x => x.SchoolName).HasMaxLength(200).IsRequired();
        builder.Property(x => x.ApplicationDisplayName).HasMaxLength(120).IsRequired();
        builder.Property(x => x.ApplicationTagline).HasMaxLength(200).IsRequired();
        builder.Property(x => x.BrandLogoPath).HasMaxLength(300);
        builder.Property(x => x.ThemePrimaryColor).HasMaxLength(7).IsRequired();
        builder.Property(x => x.ThemePrimarySoftColor).HasMaxLength(7).IsRequired();
        builder.Property(x => x.ThemeAccentColor).HasMaxLength(7).IsRequired();
        builder.Property(x => x.ThemeBackgroundColor).HasMaxLength(7).IsRequired();
        builder.Property(x => x.ThemeSurfaceColor).HasMaxLength(7).IsRequired();
        builder.Property(x => x.ThemeSidebarStartColor).HasMaxLength(7).IsRequired();
        builder.Property(x => x.ThemeSidebarEndColor).HasMaxLength(7).IsRequired();

        builder.HasOne(x => x.AcademicYearCurrent)
            .WithMany()
            .HasForeignKey(x => x.AcademicYearCurrentId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
