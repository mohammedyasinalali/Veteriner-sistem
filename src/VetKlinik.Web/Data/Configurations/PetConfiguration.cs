using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VetKlinik.Web.Domain;

namespace VetKlinik.Web.Data.Configurations;

public class PetConfiguration : IEntityTypeConfiguration<Pet>
{
    public void Configure(EntityTypeBuilder<Pet> builder)
    {
        // Türkçe harmanlama: "PAMUK" ile "Pamuk" aynı ad sayılır. Aynı sahipte ad benzersiz
        // (MCP araçları hayvanı adıyla bulur; eşzamanlı iki kayıt da veritabanında durur).
        builder.Property(p => p.Name).HasMaxLength(50).UseCollation("Turkish_CI_AS");
        builder.HasIndex(p => new { p.OwnerId, p.Name }).IsUnique();
        builder.Property(p => p.Species).HasConversion<string>().HasMaxLength(20);
        builder.Property(p => p.Breed).HasMaxLength(50);
        builder.Property(p => p.WeightKg).HasPrecision(5, 2);
        builder.Property(p => p.PhotoFileName).HasMaxLength(50);
        builder.HasOne(p => p.Owner).WithMany().HasForeignKey(p => p.OwnerId).OnDelete(DeleteBehavior.Restrict);
    }
}
