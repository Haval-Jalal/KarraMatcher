using KarraMatcher.Application.Abstractions.Audit;
using KarraMatcher.Application.Abstractions.Persistence;
using KarraMatcher.Application.Features.Teams;
using KarraMatcher.Domain.Audit;
using KarraMatcher.Domain.Matches;

namespace KarraMatcher.Application.Features.Matches.Admin;

/// <summary>
/// Tränarens matchhantering: skapa, ändra, ställa in och ta bort.
///
/// <para>
/// Samlat på ett ställe eftersom tre regler måste gälla för <em>varje</em> ändring, och
/// ingen av dem är självklar att komma ihåg en i taget.
/// </para>
///
/// <para>
/// <b>1. Kalenderprenumerationerna måste få veta.</b> En ändrad match som inte ökar
/// <c>SEQUENCE</c> uppdateras inte i föräldrarnas kalendrar (§KM.4) — appen visar rätt tid
/// medan telefonen fortsätter påminna om den gamla. Det är det värsta möjliga felet i den
/// här appen, eftersom ingen märker det förrän någon står på fel plan.
/// </para>
///
/// <para>
/// <b>2. Åtgärden ska gå att spåra.</b> "Vem flyttade matchen?" ska gå att besvara utan att
/// gissa (§KM.10). Före- och eftervärde loggas — men aldrig notisen, som är fritext.
/// </para>
///
/// <para>
/// <b>3. Laget bestäms av adressen, inte av indata.</b> Behörigheten prövas mot lagets slug
/// i routen (policyn <c>CoachOfTeam</c>). Toge kommandot emot ett lag-id i kroppen kunde en
/// tränare för Gul skicka Blås id och kringgå kontrollen helt.
/// </para>
/// </summary>
public sealed class MatchAdminService(IMatchAdminRepository matches, IAuditLog audit)
{
    /// <summary>Lägger upp en ny match i laget som adressen pekar ut.</summary>
    public async Task<MatchDto?> CreateAsync(
        string teamSlug,
        MatchDraft draft,
        Guid actorAccountId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(draft);

        var team = await matches.FindTeamBySlugAsync(teamSlug, cancellationToken)
            .ConfigureAwait(false);

        if (team is null || !await matches.VenueExistsAsync(draft.VenueId, cancellationToken)
            .ConfigureAwait(false))
        {
            return null;
        }

        var match = new Match
        {
            Id = Guid.NewGuid(),
            TeamId = team.Id,
            KickoffUtc = draft.KickoffUtc,
            OpponentName = draft.Opponent.Trim(),
            VenueId = draft.VenueId,
            IsHome = draft.IsHome,
            AddressOverride = Blank(draft.AddressOverride),
            Note = Blank(draft.Note),
            Status = MatchStatus.Scheduled,
            IcsSequence = 0,
            UpdatedUtc = DateTime.UtcNow,
        };

        await matches.AddAsync(match, cancellationToken).ConfigureAwait(false);

        await audit.RecordAsync(
            AuditActions.MatchCreated,
            actorAccountId,
            cancellationToken,
            match.Id,
            MatchSummary.Describe(match)).ConfigureAwait(false);

        await matches.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return await ReloadAsync(match.Id, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Ändrar en match. Svarar null när den inte finns eller inte hör till laget.</summary>
    public async Task<MatchDto?> UpdateAsync(
        string teamSlug,
        Guid matchId,
        MatchDraft draft,
        Guid actorAccountId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(draft);

        var match = await FindInTeamAsync(teamSlug, matchId, cancellationToken).ConfigureAwait(false);

        if (match is null || !await matches.VenueExistsAsync(draft.VenueId, cancellationToken)
            .ConfigureAwait(false))
        {
            return null;
        }

        var before = MatchSummary.Describe(match);

        match.KickoffUtc = draft.KickoffUtc;
        match.OpponentName = draft.Opponent.Trim();
        match.VenueId = draft.VenueId;
        match.IsHome = draft.IsHome;
        match.AddressOverride = Blank(draft.AddressOverride);
        match.Note = Blank(draft.Note);

        Touch(match);

        await audit.RecordAsync(
            AuditActions.MatchUpdated,
            actorAccountId,
            cancellationToken,
            match.Id,
            MatchSummary.Change(before, MatchSummary.Describe(match))).ConfigureAwait(false);

        await matches.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return match.ToDto();
    }

    /// <summary>
    /// Ställer in en match.
    ///
    /// <para>
    /// Inställd och inte raderad: kalenderposten ska bli kvar med
    /// <c>STATUS:CANCELLED</c> (§KM.4). Försvinner posten helt står den kvar i
    /// föräldrarnas kalendrar som om ingenting hänt.
    /// </para>
    /// </summary>
    public async Task<MatchDto?> CancelAsync(
        string teamSlug,
        Guid matchId,
        Guid actorAccountId,
        CancellationToken cancellationToken)
    {
        var match = await FindInTeamAsync(teamSlug, matchId, cancellationToken).ConfigureAwait(false);

        if (match is null)
        {
            return null;
        }

        var before = MatchSummary.Describe(match);

        match.Status = MatchStatus.Cancelled;
        Touch(match);

        await audit.RecordAsync(
            AuditActions.MatchCancelled,
            actorAccountId,
            cancellationToken,
            match.Id,
            MatchSummary.Change(before, MatchSummary.Describe(match))).ConfigureAwait(false);

        await matches.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return match.ToDto();
    }

    /// <summary>
    /// Tar bort en match helt.
    ///
    /// <para>
    /// För en match som aldrig skulle ha lagts in. En match som ställts in ska ställas in,
    /// inte raderas — se <see cref="CancelAsync"/>.
    /// </para>
    /// </summary>
    public async Task<bool> DeleteAsync(
        string teamSlug,
        Guid matchId,
        Guid actorAccountId,
        CancellationToken cancellationToken)
    {
        var match = await FindInTeamAsync(teamSlug, matchId, cancellationToken).ConfigureAwait(false);

        if (match is null)
        {
            return false;
        }

        await audit.RecordAsync(
            AuditActions.MatchDeleted,
            actorAccountId,
            cancellationToken,
            match.Id,
            MatchSummary.Describe(match)).ConfigureAwait(false);

        matches.Remove(match);

        await matches.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return true;
    }

    /// <summary>
    /// Läser om matchen med spelplatsen inläst, så svaret får med adress och koordinater.
    ///
    /// <para>
    /// En nyskapad match har bara ett spelplats-id, inte spelplatsen. Att svara utan den
    /// hade gett tränaren ett kort utan adress direkt efter att hen fyllt i en.
    /// </para>
    /// </summary>
    private async Task<MatchDto?> ReloadAsync(Guid matchId, CancellationToken cancellationToken)
    {
        var saved = await matches.FindForUpdateAsync(matchId, cancellationToken).ConfigureAwait(false);

        return saved?.ToDto();
    }

    /// <summary>
    /// Matchen, men bara om den hör till laget i adressen.
    ///
    /// <para>
    /// Objektnivå-auktorisering (checklistan 2.6). Policyn har redan slagit fast att
    /// anroparen är tränare för <em>laget</em> — den här kontrollen slår fast att
    /// <em>matchen</em> hör dit. Utan den kunde en tränare för Gul ändra en match i Blå
    /// genom att skicka Blås match-id till sin egen lagadress.
    /// </para>
    /// </summary>
    private async Task<Match?> FindInTeamAsync(
        string teamSlug,
        Guid matchId,
        CancellationToken cancellationToken)
    {
        var match = await matches.FindForUpdateAsync(matchId, cancellationToken).ConfigureAwait(false);

        return match?.Team?.Slug == teamSlug ? match : null;
    }

    /// <summary>
    /// Märker matchen som ändrad.
    ///
    /// <para>
    /// <c>IcsSequence</c> ökas vid varje ändring. Utan det uppdateras inte föräldrarnas
    /// kalendrar (§KM.4), och appen visar en tid telefonen inte känner till.
    /// </para>
    /// </summary>
    private static void Touch(Match match)
    {
        match.IcsSequence++;
        match.UpdatedUtc = DateTime.UtcNow;
    }

    /// <summary>Tom text räknas som "inget värde" — inte som ett värde som är tomt.</summary>
    private static string? Blank(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

/// <summary>
/// Det en tränare fyller i om en match.
///
/// <para>
/// Laget finns med flit inte här — det bestäms av adressen, som är det behörigheten prövas
/// mot. Se klassdokumentationen för <see cref="MatchAdminService"/>.
/// </para>
/// </summary>
public sealed record MatchDraft(
    DateTime KickoffUtc,
    string Opponent,
    Guid VenueId,
    bool IsHome,
    string? AddressOverride,
    string? Note);
