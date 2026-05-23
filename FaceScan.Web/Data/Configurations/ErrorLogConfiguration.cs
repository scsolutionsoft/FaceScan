using FaceScan.Web.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FaceScan.Web.Data.Configurations;

public class ErrorLogConfiguration : IEntityTypeConfiguration<ErrorLog>
{
    public void Configure(EntityTypeBuilder<ErrorLog> builder)
    {
        builder.ToTable("ErrorLogs");
        builder.Property(x => x.Message).HasMaxLength(2000).IsRequired();
        builder.Property(x => x.StackTrace).HasMaxLength(8000);
        builder.Property(x => x.Source).HasMaxLength(400);
        builder.HasIndex(x => x.LoggedAt);
    }
}
