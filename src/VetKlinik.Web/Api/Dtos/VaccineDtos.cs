using VetKlinik.Web.Domain;
using VetKlinik.Web.Services;

namespace VetKlinik.Web.Api.Dtos;

public record VaccineRecordDto(int Id, string VaccineName, DateOnly AdministeredOn, DateOnly? NextDueOn, string? VeterinarianName)
{
    public static VaccineRecordDto From(VaccineRecord r) =>
        new(r.Id, r.VaccineName, r.AdministeredOn, r.NextDueOn, r.Veterinarian?.User.FullName);
}

public record UpcomingVaccineDto(string VaccineName, DateOnly DueOn, int DaysLeft, bool IsOverdue)
{
    public static UpcomingVaccineDto From(UpcomingVaccine u) => new(u.VaccineName, u.DueOn, u.DaysLeft, u.IsOverdue);
}

public record PetVaccinesDto(
    int PetId, string PetName, IReadOnlyList<VaccineRecordDto> History, IReadOnlyList<UpcomingVaccineDto> Upcoming)
{
    public static PetVaccinesDto From(VaccineSchedule s) => new(
        s.Pet.Id,
        s.Pet.Name,
        s.History.Select(VaccineRecordDto.From).ToList(),
        s.Upcoming.Select(UpcomingVaccineDto.From).ToList());
}
