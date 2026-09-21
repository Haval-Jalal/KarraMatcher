using KarraMatcher.Application.Abstractions.Persistence;
using KarraMatcher.Domain.Push;

namespace KarraMatcher.Application.Features.Push;

/// <summary>
/// En förälders notisinställningar per lag (`#65`).
///
/// <para>
/// Läser och sätter vilka sorters notiser kontot vill ha för ett lag. Finns ingen rad är
/// allt på — förvalet — och en sådan läsning skapar ingen rad; en rad skrivs först när
/// någon ändrar något.
/// </para>
/// </summary>
public sealed class NotificationSettingsService(
    ITeamRepository teams,
    INotificationPreferenceRepository preferences,
    TimeProvider clock)
{
    /// <summary>Kontots inställning för laget. Null när laget inte finns.</summary>
    public async Task<NotificationSettingsDto?> GetAsync(
        string slug,
        Guid accountId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(slug);

        var team = await teams.FindBySlugAsync(slug, cancellationToken).ConfigureAwait(false);

        if (team is null)
        {
            return null;
        }

        var preference = await preferences.FindAsync(accountId, team.Id, cancellationToken)
            .ConfigureAwait(false);

        return preference is null
            ? new NotificationSettingsDto(true, true, true, true)
            : NotificationSettingsDto.For(preference);
    }

    /// <summary>Sätter kontots inställning för laget. Null när laget inte finns.</summary>
    public async Task<NotificationSettingsDto?> SetAsync(
        string slug,
        Guid accountId,
        NotificationSettingsDraft draft,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(slug);
        ArgumentNullException.ThrowIfNull(draft);

        var team = await teams.FindBySlugAsync(slug, cancellationToken).ConfigureAwait(false);

        if (team is null)
        {
            return null;
        }

        var preference = await preferences.FindAsync(accountId, team.Id, cancellationToken)
            .ConfigureAwait(false);

        var now = clock.GetUtcNow().UtcDateTime;

        if (preference is null)
        {
            preference = new NotificationPreference
            {
                Id = Guid.NewGuid(),
                AccountId = accountId,
                TeamId = team.Id,
            };

            await preferences.AddAsync(preference, cancellationToken).ConfigureAwait(false);
        }

        preference.EventChanges = draft.EventChanges;
        preference.Kallelser = draft.Kallelser;
        preference.Carpool = draft.Carpool;
        preference.Chat = draft.Chat;
        preference.UpdatedUtc = now;

        await preferences.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return NotificationSettingsDto.For(preference);
    }
}
