using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using VetKlinik.Web.Domain;

namespace VetKlinik.Web.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : IdentityDbContext<ApplicationUser>(options)
{
    public DbSet<Clinic> Clinics => Set<Clinic>();
    public DbSet<Service> Services => Set<Service>();
    public DbSet<Specialty> Specialties => Set<Specialty>();
    public DbSet<Veterinarian> Veterinarians => Set<Veterinarian>();
    public DbSet<Pet> Pets => Set<Pet>();
    public DbSet<VaccineRecord> VaccineRecords => Set<VaccineRecord>();
    public DbSet<Appointment> Appointments => Set<Appointment>();
    public DbSet<AccessToken> AccessTokens => Set<AccessToken>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
