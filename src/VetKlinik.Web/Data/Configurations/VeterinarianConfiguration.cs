using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VetKlinik.Web.Domain;

namespace VetKlinik.Web.Data.Configurations;

public class VeterinarianConfiguration : IEntityTypeConfiguration<Veterinarian>
{
    public void Configure(EntityTypeBuilder<Veterinarian> builder)
    {
        builder.Property(v => v.Title).HasMaxLength(50);
        builder.HasIndex(v => v.UserId).IsUnique();
        builder.HasOne(v => v.User).WithMany().HasForeignKey(v => v.UserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(v => v.Services).WithMany(s => s.Veterinarians)
            .UsingEntity(join => join.ToTable("VeterinarianServices"));
        builder.HasMany(v => v.Specialties).WithMany(s => s.Veterinarians)
            .UsingEntity(join => join.ToTable("VeterinarianSpecialties"));
        builder.HasMany(v => v.Availabilities).WithOne().HasForeignKey(a => a.VeterinarianId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
