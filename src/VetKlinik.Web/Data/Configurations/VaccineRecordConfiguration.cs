using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VetKlinik.Web.Domain;

namespace VetKlinik.Web.Data.Configurations;

public class VaccineRecordConfiguration : IEntityTypeConfiguration<VaccineRecord>
{
    public void Configure(EntityTypeBuilder<VaccineRecord> builder)
    {
        builder.Property(v => v.VaccineName).HasMaxLength(100);
        builder.HasOne(v => v.Pet).WithMany(p => p.VaccineRecords).HasForeignKey(v => v.PetId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(v => v.Veterinarian).WithMany().HasForeignKey(v => v.VeterinarianId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
