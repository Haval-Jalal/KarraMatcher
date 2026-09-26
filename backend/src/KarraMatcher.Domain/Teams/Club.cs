namespace KarraMatcher.Domain.Teams;

/// <summary>Föreningen. En enda i dag, men modellen är förberedd för fler.</summary>
public sealed class Club
{
    public Guid Id { get; set; }

    public required string Name { get; set; }

    /// <summary>Används i URL:er. Små bokstäver, inga svenska tecken.</summary>
    public required string Slug { get; set; }

    /// <summary>
    /// Klubbens hemmaplan (`#307`): namn och adress som en tränare i klubben skriver in i
    /// inställningarna. Koordinaterna geokodas ur adressen så väder och vägbeskrivning fungerar.
    /// Null tills någon satt den — då kan en hemma-händelse inte skapas förrän platsen finns.
    /// Delas av alla klubbens truppar; det är klubbens plan, inte truppens.
    /// </summary>
    public string? HomeVenueName { get; set; }

    public string? HomeAddress { get; set; }

    public double? HomeLatitude { get; set; }

    public double? HomeLongitude { get; set; }

    public ICollection<AgeGroup> AgeGroups { get; } = [];
}
