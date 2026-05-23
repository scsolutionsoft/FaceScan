using FaceScan.Web.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FaceScan.Web.Data.Configurations;

public class ClassPeriodConfiguration : IEntityTypeConfiguration<ClassPeriod>
{
    public void Configure(EntityTypeBuilder<ClassPeriod> builder)
    {
        builder.ToTable("ClassPeriods");

        builder.Property(x => x.Name).HasMaxLength(80).IsRequired();

        builder.HasIndex(x => x.SortOrder).IsUnique();
    }
}
