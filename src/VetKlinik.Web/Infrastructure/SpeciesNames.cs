using VetKlinik.Web.Domain;

namespace VetKlinik.Web.Infrastructure;

/// <summary>Hayvan türlerinin Türkçe adları.</summary>
public static class SpeciesNames
{
    public static string Name(Species species) => species switch
    {
        Species.Dog => "Köpek",
        Species.Cat => "Kedi",
        Species.Bird => "Kuş",
        Species.Rabbit => "Tavşan",
        Species.Reptile => "Sürüngen",
        _ => "Diğer",
    };
}
