using KarraMatcher.Domain.Attendance;
using KarraMatcher.Infrastructure.Persistence;

namespace KarraMatcher.Api.Integration.Tests;

/// <summary>
/// Seed-hjälp för kallelsen. Matcher grindas numera av kallelse (§KM.7, ägarbeslut `#514`): bara
/// den som sköter laget/truppen eller vars barn är <em>kallat</em> når matchen och dess
/// samåkning/kallelse-svar. Tester som förr förutsatte att vilken lag-medlem som helst nådde
/// matchen kallar därför barnen först via den här.
/// </summary>
internal static class AttendanceSeed
{
    /// <summary>
    /// Öppnar en kallelse för matchen och bjuder in de angivna barnen, så deras vårdnadshavare når
    /// matchen. Lägger till i kontexten; anroparen sparar. <paramref name="openedByAccountId"/> är
    /// en audit-not utan främmande nyckel (§KM.6), så vilket konto-id som helst duger.
    /// </summary>
    public static void CallChildrenToMatch(
        KarraMatcherDbContext context,
        Guid matchId,
        Guid openedByAccountId,
        params Guid[] childIds)
    {
        var call = new AttendanceCall
        {
            Id = Guid.NewGuid(),
            MatchId = matchId,
            OpenedByAccountId = openedByAccountId,
            OpenedUtc = DateTime.UtcNow,
        };

        context.AttendanceCalls.Add(call);

        foreach (var childId in childIds)
        {
            context.AttendanceInvitations.Add(new AttendanceInvitation
            {
                Id = Guid.NewGuid(),
                CallId = call.Id,
                ChildId = childId,
            });
        }
    }
}
