using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VetKlinik.Web.Domain;

namespace VetKlinik.Web.Data.Configurations;

public class VetAvailabilityConfiguration : IEntityTypeConfiguration<VetAvailability>
{
    public void Configure(EntityTypeBuilder<VetAvailability> builder)
    {
        builder.ToTable("VetAvailabilities");
        builder.Property(a => a.Day).HasConversion<string>().HasMaxLength(10);
        builder.Property(a => a.Starts).HasPrecision(0);
        builder.Property(a => a.Ends).HasPrecision(0);
    }
}
