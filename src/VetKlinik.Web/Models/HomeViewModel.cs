using VetKlinik.Web.Domain;

namespace VetKlinik.Web.Models;

public record HomeViewModel(Clinic Clinic, IReadOnlyList<Service> Services);
