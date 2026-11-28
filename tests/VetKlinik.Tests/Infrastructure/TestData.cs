using Microsoft.AspNetCore.Identity;
using VetKlinik.Web.Data;
using VetKlinik.Web.Domain;

namespace VetKlinik.Tests.Infrastructure;

public sealed record Seeded(
    ApplicationUser Admin,
    ApplicationUser OwnerA, ApplicationUser OwnerB,
    Pet PetA, Pet PetB,
    Veterinarian VetA, Veterinarian VetB,
    Service Examination, Service Neutering)
{
    public Appointment NewAppointment(DateTime startsAt) => new()
    {
        PetId = PetA.Id,
        VeterinarianId = VetA.Id,
        ServiceId = Examination.Id,
        StartsAt = startsAt,
        EndsAt = startsAt.AddMinutes(Examination.DurationMinutes),
        DurationMinutes = Examination.DurationMinutes,
        Price = Examination.Price,
        Status = AppointmentStatus.Pending,
        CreatedAt = startsAt.AddDays(-1),
    };
}

public static class TestData
{
    public const string Password = "Test1234!";

    public static async Task<Seeded> SeedAsync(AppDbContext db)
    {
        var admin = NewUser("admin@test.local", "Test Admin");
        var ownerA = NewUser("ownera@test.local", "Üye A");
        var ownerB = NewUser("ownerb@test.local", "Üye B");
        var vetUserA = NewUser("veta@test.local", "Hekim A");
        var vetUserB = NewUser("vetb@test.local", "Hekim B");
        db.Users.AddRange(admin, ownerA, ownerB, vetUserA, vetUserB);

        AddToRole(db, "Admin", admin);
        AddToRole(db, "Veteriner", vetUserA, vetUserB);
        AddToRole(db, "Uye", ownerA, ownerB);

        db.Clinics.Add(new Clinic
        {
            Name = "Test Kliniği",
            Address = "Sakarya",
            Phone = "0264 000 00 00",
            Hours = Enum.GetValues<DayOfWeek>().Select(day => new ClinicHours
            {
                Day = day,
                IsClosed = !IsWeekday(day),
                Opens = new TimeOnly(9, 0),
                Closes = new TimeOnly(18, 0),
            }).ToList(),
        });

        var examination = new Service { Name = "Muayene", DurationMinutes = 30, Price = 500m, IsActive = true };
        var neutering = new Service { Name = "Kısırlaştırma", DurationMinutes = 90, Price = 3000m, IsActive = true };
        var vetA = NewVet(vetUserA, examination, neutering);
        vetA.Specialties.Add(new Specialty { Name = "Cerrahi" });
        var vetB = NewVet(vetUserB, examination);
        db.Veterinarians.AddRange(vetA, vetB);

        var petA = NewPet(ownerA, "Pamuk");
        var petB = NewPet(ownerB, "Karabaş");
        db.Pets.AddRange(petA, petB);

        await db.SaveChangesAsync();
        return new Seeded(admin, ownerA, ownerB, petA, petB, vetA, vetB, examination, neutering);
    }

    private static bool IsWeekday(DayOfWeek day) => day is not (DayOfWeek.Saturday or DayOfWeek.Sunday);

    private static ApplicationUser NewUser(string email, string fullName)
    {
        var user = new ApplicationUser
        {
            UserName = email,
            NormalizedUserName = email.ToUpperInvariant(),
            Email = email,
            NormalizedEmail = email.ToUpperInvariant(),
            EmailConfirmed = true,
            LockoutEnabled = true,
            FullName = fullName,
            SecurityStamp = Guid.NewGuid().ToString(),
        };
        user.PasswordHash = new PasswordHasher<ApplicationUser>().HashPassword(user, Password);
        return user;
    }

    private static void AddToRole(AppDbContext db, string roleName, params ApplicationUser[] users)
    {
        var role = new IdentityRole(roleName) { NormalizedName = roleName.ToUpperInvariant() };
        db.Roles.Add(role);
        db.UserRoles.AddRange(users.Select(u => new IdentityUserRole<string> { UserId = u.Id, RoleId = role.Id }));
    }

    private static Veterinarian NewVet(ApplicationUser user, params Service[] services) => new()
    {
        User = user,
        Title = "Vet. Hek.",
        IsActive = true,
        Services = [.. services],
        Availabilities = Enum.GetValues<DayOfWeek>().Where(IsWeekday).Select(day => new VetAvailability
        {
            Day = day,
            Starts = new TimeOnly(9, 0),
            Ends = new TimeOnly(18, 0),
        }).ToList(),
    };

    private static Pet NewPet(ApplicationUser owner, string name) => new()
    {
        Owner = owner,
        Name = name,
        Species = Species.Cat,
        Breed = "Tekir",
        BirthDate = new DateOnly(2024, 5, 1),
        WeightKg = 4.5m,
    };
}
