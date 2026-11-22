using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VetKlinik.Web.Domain;

namespace VetKlinik.Web.Data.Configurations;

public class ClinicConfiguration : IEntityTypeConfiguration<Clinic>
{
    public void Configure(EntityTypeBuilder<Clinic> builder)
    {
        builder.Property(c => c.Name).HasMaxLength(100);
        builder.Property(c => c.Address).HasMaxLength(250);
        builder.Property(c => c.Phone).HasMaxLength(20);
        builder.HasMany(c => c.Hours).WithOne().HasForeignKey(h => h.ClinicId).OnDelete(DeleteBehavior.Cascade);
    }
}
