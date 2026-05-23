using FaceScan.Web.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FaceScan.Web.Data.Configurations;

public class TeacherFaceProfileConfiguration : IEntityTypeConfiguration<TeacherFaceProfile>
{
    public void Configure(EntityTypeBuilder<TeacherFaceProfile> builder)
    {
        builder.ToTable("TeacherFaceProfiles");

        builder.Property(x => x.UserId).HasMaxLength(450).IsRequired();
        builder.Property(x => x.TemplateVersion).HasMaxLength(50).IsRequired();
        builder.Property(x => x.EmbeddingJson).HasMaxLength(4000);
        builder.Property(x => x.QualityNote).HasMaxLength(500);

        builder.HasOne(x => x.User)
            .WithOne(x => x.TeacherFaceProfile)
            .HasForeignKey<TeacherFaceProfile>(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => x.UserId).IsUnique();
    }
}
