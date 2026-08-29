namespace KarraMatcher.Domain.Matches;

/// <summary>
/// En spelplats. Koordinaterna driver väderprognosen och adressen kartlänken —
/// därför är de en del av modellen och inte fritext på matchen.
/// </summary>
public sealed class Venue
{
    public Guid Id { get; set; }

    public required string Name { get; set; }

    public required string Address { get; set; }

    public double Latitude { get; set; }

    public double Longitude { get; set; }

    /// <summary>Sant för klubbens egen plan.</summary>
    public bool IsHome { get; set; }

    public ICollection<Match> Matches { get; } = [];
}
