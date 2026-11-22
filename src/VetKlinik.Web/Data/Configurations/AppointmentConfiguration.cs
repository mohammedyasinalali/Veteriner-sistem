using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VetKlinik.Web.Domain;

namespace VetKlinik.Web.Data.Configurations;

public class AppointmentConfiguration : IEntityTypeConfiguration<Appointment>
{
    public void Configure(EntityTypeBuilder<Appointment> builder)
    {
        builder.Property(a => a.StartsAt).HasPrecision(0);
        builder.Property(a => a.EndsAt).HasPrecision(0);
        builder.Property(a => a.CreatedAt).HasPrecision(0);
        builder.Property(a => a.Price).HasPrecision(10, 2);
        builder.Property(a => a.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(a => a.RejectionReason).HasMaxLength(500);
        builder.Property(a => a.ExaminationNote).HasMaxLength(2000);
        builder.Property(a => a.RowVersion).IsRowVersion();
        builder.HasIndex(a => new { a.VeterinarianId, a.StartsAt });

        // Randevu geçmişi korunur: randevusu olan kayıtlar silinemez.
        builder.HasOne(a => a.Pet).WithMany(p => p.Appointments).HasForeignKey(a => a.PetId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(a => a.Veterinarian).WithMany().HasForeignKey(a => a.VeterinarianId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(a => a.Service).WithMany().HasForeignKey(a => a.ServiceId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
