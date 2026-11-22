using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VetKlinik.Web.Domain;

namespace VetKlinik.Web.Data.Configurations;

public class PetConfiguration : IEntityTypeConfiguration<Pet>
{
    public void Configure(EntityTypeBuilder<Pet> builder)
    {
        builder.Property(p => p.Name).HasMaxLength(50);
        builder.Property(p => p.Species).HasConversion<string>().HasMaxLength(20);
        builder.Property(p => p.Breed).HasMaxLength(50);
        builder.Property(p => p.WeightKg).HasPrecision(5, 2);
        builder.Property(p => p.PhotoFileName).HasMaxLength(50);
        builder.HasOne(p => p.Owner).WithMany().HasForeignKey(p => p.OwnerId).OnDelete(DeleteBehavior.Restrict);
    }
}
