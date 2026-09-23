using KarraMatcher.Application.Features.Carpool;
using KarraMatcher.Application.Features.Push;

namespace KarraMatcher.Application.Tests;

/// <summary>
/// Samåkningsnotisernas djuplänk (§KM.12, `#244`). Samåkningen bär internt <c>MatchId</c>, men
/// det pekar på en händelse sedan `#198` — så länken måste vara <c>/handelse/{id}</c>, den vy
/// klienten faktiskt har. Den utgångna <c>/match/</c>-adressen 404:ar och lämnar en förälder
/// på en död sida efter ett klick på notisen.
/// </summary>
public sealed class CarpoolNotificationTests
{
    private static readonly Guid MatchId = Guid.Parse("11111111-2222-3333-4444-555555555555");

    public static TheoryData<PushMessage> AllMessages() =>
        new()
        {
            CarpoolNotification.NewOffer(MatchId),
            CarpoolNotification.NewRequest(MatchId),
            CarpoolNotification.RequestAnswered(MatchId),
            CarpoolNotification.OfferWithdrawn(MatchId),
        };

    [Theory]
    [MemberData(nameof(AllMessages))]
    public void Notiserna_PekarPaHandelsesidan_InteDenUtgangnaMatchAdressen(PushMessage message)
    {
        Assert.Equal($"/handelse/{MatchId}", message.Url);
    }
}
