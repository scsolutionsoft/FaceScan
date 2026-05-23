using FaceScan.Web.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FaceScan.Web.Data.Configurations;

public class EdgeAgentHeartbeatConfiguration : IEntityTypeConfiguration<EdgeAgentHeartbeat>
{
    public void Configure(EntityTypeBuilder<EdgeAgentHeartbeat> builder)
    {
        builder.ToTable("EdgeAgentHeartbeats");

        builder.Property(x => x.StationCode).HasMaxLength(50).IsRequired();
        builder.Property(x => x.AgentId).HasMaxLength(120).IsRequired();
        builder.Property(x => x.LastSeenAtUtc).IsRequired();
        builder.Property(x => x.LastMessage).HasMaxLength(300);
        builder.Property(x => x.LastIpAddress).HasMaxLength(64);

        builder.HasIndex(x => new { x.StationCode, x.AgentId }).IsUnique();
        builder.HasIndex(x => x.LastSeenAtUtc);
    }
}