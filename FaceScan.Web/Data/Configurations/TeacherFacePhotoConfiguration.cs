using FaceScan.Web.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FaceScan.Web.Data.Configurations;

public class TeacherFacePhotoConfiguration : IEntityTypeConfiguration<TeacherFacePhoto>
{
    public void Configure(EntityTypeBuilder<TeacherFacePhoto> builder)
    {
        builder.ToTable("TeacherFacePhotos");

        builder.Property(x => x.UserId).HasMaxLength(450).IsRequired();
        builder.Property(x => x.FilePath).HasMaxLength(350).IsRequired();
        builder.Property(x => x.FileName).HasMaxLength(255).IsRequired();
        builder.Property(x => x.ContentType).HasMaxLength(100).IsRequired();
        builder.Property(x => x.QualityScore).HasColumnType("decimal(5,2)");

        builder.HasOne(x => x.User)
            .WithMany(x => x.TeacherFacePhotos)
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => x.UserId);
    }
}
