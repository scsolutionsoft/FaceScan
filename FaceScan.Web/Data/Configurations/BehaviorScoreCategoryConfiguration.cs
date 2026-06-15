using FaceScan.Web.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FaceScan.Web.Data.Configurations;

public class BehaviorScoreCategoryConfiguration : IEntityTypeConfiguration<BehaviorScoreCategory>
{
    public void Configure(EntityTypeBuilder<BehaviorScoreCategory> builder)
    {
        builder.ToTable("BehaviorScoreCategories");
        builder.Property(x => x.Name).HasMaxLength(120).IsRequired();
        builder.Property(x => x.Description).HasMaxLength(500);
        builder.HasIndex(x => x.Name).IsUnique();
    }
}
