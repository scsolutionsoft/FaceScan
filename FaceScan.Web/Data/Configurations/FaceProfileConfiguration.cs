using FaceScan.Web.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FaceScan.Web.Data.Configurations;

public class FaceProfileConfiguration : IEntityTypeConfiguration<FaceProfile>
{
    public void Configure(EntityTypeBuilder<FaceProfile> builder)
    {
        builder.ToTable("FaceProfiles");

        builder.Property(x => x.TemplateVersion).HasMaxLength(50).IsRequired();
        builder.Property(x => x.EmbeddingJson).HasMaxLength(4000);
        builder.Property(x => x.QualityNote).HasMaxLength(500);

        builder.HasOne(x => x.Student)
            .WithOne(x => x.FaceProfile)
            .HasForeignKey<FaceProfile>(x => x.StudentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => x.StudentId).IsUnique();
    }
}
