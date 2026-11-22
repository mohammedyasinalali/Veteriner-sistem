using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VetKlinik.Web.Domain;

namespace VetKlinik.Web.Data.Configurations;

public class ClinicHoursConfiguration : IEntityTypeConfiguration<ClinicHours>
{
    public void Configure(EntityTypeBuilder<ClinicHours> builder)
    {
        builder.ToTable("ClinicHours");
        builder.Property(h => h.Day).HasConversion<string>().HasMaxLength(10);
        builder.Property(h => h.Opens).HasPrecision(0);
        builder.Property(h => h.Closes).HasPrecision(0);
        builder.HasIndex(h => new { h.ClinicId, h.Day }).IsUnique();
    }
}
