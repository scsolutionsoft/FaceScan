using FaceScan.Web.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FaceScan.Web.Data.Configurations;

public class WasteBankRateConfiguration : IEntityTypeConfiguration<WasteBankRate>
{
    public void Configure(EntityTypeBuilder<WasteBankRate> builder)
    {
        builder.ToTable("WasteBankRates");
        builder.Property(x => x.WasteType).HasMaxLength(160).IsRequired();
        builder.Property(x => x.PricePerKg).HasColumnType("decimal(10,2)");

        builder.HasIndex(x => new { x.WasteType, x.IsActive });
        builder.HasIndex(x => x.EffectiveFrom);
    }
}
