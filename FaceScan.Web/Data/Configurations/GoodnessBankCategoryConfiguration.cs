using FaceScan.Web.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FaceScan.Web.Data.Configurations;

public class GoodnessBankCategoryConfiguration : IEntityTypeConfiguration<GoodnessBankCategory>
{
    public void Configure(EntityTypeBuilder<GoodnessBankCategory> builder)
    {
        builder.ToTable("GoodnessBankCategories");
        builder.Property(x => x.Name).HasMaxLength(120).IsRequired();
        builder.Property(x => x.Description).HasMaxLength(500);
        builder.HasIndex(x => x.Name).IsUnique();
    }
}
