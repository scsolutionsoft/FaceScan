using FaceScan.Web.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FaceScan.Web.Data.Configurations;

public class StudentGuardianConfiguration : IEntityTypeConfiguration<StudentGuardian>
{
    public void Configure(EntityTypeBuilder<StudentGuardian> builder)
    {
        builder.ToTable("StudentGuardians");
        builder.Property(x => x.FullName).HasMaxLength(200).IsRequired();
        builder.Property(x => x.NationalId).HasMaxLength(20);
        builder.Property(x => x.Relationship).HasMaxLength(120);
        builder.Property(x => x.PhoneNumber).HasMaxLength(40);
        builder.Property(x => x.Occupation).HasMaxLength(160);
        builder.Property(x => x.MonthlyIncome).HasColumnType("decimal(12,2)");
        builder.Property(x => x.Address).HasMaxLength(500);
        builder.Property(x => x.PhotoPath).HasMaxLength(300);

        builder.HasIndex(x => x.StudentId);
        builder.HasIndex(x => x.NationalId);

        builder.HasOne(x => x.Student)
            .WithMany()
            .HasForeignKey(x => x.StudentId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
