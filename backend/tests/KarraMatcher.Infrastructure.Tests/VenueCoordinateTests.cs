using KarraMatcher.Infrastructure.Persistence.Seed;

namespace KarraMatcher.Infrastructure.Tests;

/// <summary>
/// Spelplatsernas koordinater driver väderprognosen (#22).
///
/// <para>
/// De var en gång avrundade till två decimaler och låg upp till 2,2 kilometer fel. Vid
/// kusten är två kilometer skillnaden mellan regn och uppehåll, och både Öckerö och
/// Kungälv ligger vid vatten. En förälder som lämnar regnkläderna hemma för att appen sa
/// uppehåll är precis det funktionen finns för att förhindra.
/// </para>
///
/// <para>
/// Testerna nedan fäller bygget om någon skriver in grova värden igen.
/// </para>
/// </summary>
public class VenueCoordinateTests
{
    /// <summary>
    /// Hur grovt ett värde får vara. Två decimaler är ungefär en kilometers upplösning i
    /// våra breddgrader, vilket är precis den storleksordning som gjorde prognosen fel.
    /// </summary>
    private const int CoarseDecimals = 2;

    /// <summary>
    /// Spelplatser vars position ännu inte gått att verifiera.
    ///
    /// <para>
    /// OpenStreetMap har fyra namnlösa fotbollsplaner i Kode och ingen namngiven
    /// idrottsplats, så den raden står kvar med sitt gamla avrundade värde. Ett gissat
    /// värde hade sett lika rätt ut som ett verifierat och varit omöjligt att skilja från
    /// det efteråt.
    /// </para>
    ///
    /// <para>
    /// Listan är avsiktligt namngiven och inte ett generellt undantag: växer den märks det
    /// i en granskning.
    /// </para>
    /// </summary>
    private static readonly string[] Unverified = ["Kode IP 31"];

    /// <summary>
    /// Sant om värdet ser avrundat till hundradelar ut.
    ///
    /// <para>
    /// Att räkna decimaler i strängen fungerar inte: <c>11.9840</c> är samma double som
    /// <c>11.984</c>, och den efterföljande nollan finns helt enkelt inte. Ett avrundat
    /// värde känns i stället igen på att det är <em>identiskt</em> med sin egen avrundning
    /// till två decimaler — 11,99 är det, 11,984 är det inte.
    /// </para>
    /// </summary>
    private static bool IsCoarse(double value) =>
        Math.Abs(value - Math.Round(value, CoarseDecimals)) < double.Epsilon;

    [Fact]
    public void Spelplatser_HarKoordinaterMedTillrackligPrecision()
    {
        var grova = SeedData.Venues
            .Where(venue => !Unverified.Contains(venue.Name, StringComparer.Ordinal))
            .Where(venue => IsCoarse(venue.Latitude) && IsCoarse(venue.Longitude))
            .Select(venue => $"{venue.Name}: {venue.Latitude}, {venue.Longitude}")
            .ToArray();

        Assert.True(
            grova.Length == 0,
            $"Spelplatser med för grov position:{Environment.NewLine}"
                + string.Join(Environment.NewLine, grova.Select(v => "  - " + v))
                + $"{Environment.NewLine}Koordinaterna driver väderprognosen. Två decimaler "
                + "är ungefär en kilometers upplösning, vilket vid kusten är skillnaden "
                + "mellan regn och uppehåll. Verifiera positionen i stället för att avrunda.");
    }

    [Fact]
    public void Spelplatser_LiggerIVastsverige()
    {
        // En förväxlad latitud och longitud ger koordinater i Somalia och ett väder som
        // ser rimligt ut i siffror. Grovkontrollen fångar det direkt.
        var utanfor = SeedData.Venues
            .Where(venue =>
                venue.Latitude is < 57.0 or > 59.0 || venue.Longitude is < 11.0 or > 13.0)
            .Select(venue => $"{venue.Name}: {venue.Latitude}, {venue.Longitude}")
            .ToArray();

        Assert.True(
            utanfor.Length == 0,
            "Spelplatser utanför Västsverige: " + string.Join(", ", utanfor));
    }

    [Fact]
    public void Overifierade_ArFortfarandeBaraKodeIp()
    {
        // Skyddar undantagslistan mot att växa i tysthet. Blir Kode IP verifierad ska den
        // här raden tas bort i samma PR.
        Assert.Equal(["Kode IP 31"], Unverified);
    }
}
