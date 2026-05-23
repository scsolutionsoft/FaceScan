using FaceScan.Web.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FaceScan.Web.Data.Configurations;

public class StudentPhotoConfiguration : IEntityTypeConfiguration<StudentPhoto>
{
    public void Configure(EntityTypeBuilder<StudentPhoto> builder)
    {
        builder.ToTable("StudentPhotos");
        builder.Property(x => x.FilePath).HasMaxLength(350).IsRequired();
        builder.Property(x => x.FileName).HasMaxLength(255).IsRequired();
        builder.Property(x => x.ContentType).HasMaxLength(100).IsRequired();
        builder.Property(x => x.QualityScore).HasColumnType("decimal(5,2)");

        builder.HasOne(x => x.Student)
            .WithMany(x => x.StudentPhotos)
            .HasForeignKey(x => x.StudentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => x.StudentId);
    }
}
