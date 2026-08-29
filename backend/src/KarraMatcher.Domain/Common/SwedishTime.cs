namespace KarraMatcher.Domain.Common;

/// <summary>
/// Enda stället i backend där svensk lokaltid möter UTC (§KM.5).
///
/// Klubben spelar i svensk tid, databasen lagrar UTC. Säsongen sträcker sig förbi
/// sommartidsskiftet i oktober, och en match som visas en timme fel är precis den
/// sortens fel som får folk att sluta lita på appen.
/// </summary>
public static class SwedishTime
{
    public static TimeZoneInfo Zone { get; } =
        TimeZoneInfo.FindSystemTimeZoneById("Europe/Stockholm");

    /// <summary>
    /// Tolkar ett datum och en tid som svensk lokaltid och ger motsvarande UTC.
    /// </summary>
    /// <exception cref="ArgumentException">
    /// Tiden finns inte — den ligger i timmen som hoppas över när klockan ställs fram.
    /// Det är alltid felaktig indata, aldrig något att gissa sig förbi.
    /// </exception>
    public static DateTime ToUtc(DateOnly date, TimeOnly time)
    {
        var local = date.ToDateTime(time, DateTimeKind.Unspecified);

        if (Zone.IsInvalidTime(local))
        {
            throw new ArgumentException(
                $"Tiden {local:yyyy-MM-dd HH:mm} finns inte i svensk tid — klockan "
                + "ställs fram över den timmen.", nameof(time));
        }

        if (Zone.IsAmbiguousTime(local))
        {
            // Hösten då klockan ställs tillbaka inträffar tiden två gånger. Vi väljer
            // den första förekomsten (sommartid), vilket är den tidigare tidpunkten.
            var offsets = Zone.GetAmbiguousTimeOffsets(local);
            var summer = offsets.Max();
            return DateTime.SpecifyKind(local - summer, DateTimeKind.Utc);
        }

        return TimeZoneInfo.ConvertTimeToUtc(local, Zone);
    }

    /// <summary>Ger svensk lokaltid för en UTC-tidpunkt.</summary>
    public static DateTime ToSwedish(DateTime utc)
    {
        if (utc.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("Tidpunkten måste vara UTC.", nameof(utc));
        }

        return TimeZoneInfo.ConvertTimeFromUtc(utc, Zone);
    }
}
