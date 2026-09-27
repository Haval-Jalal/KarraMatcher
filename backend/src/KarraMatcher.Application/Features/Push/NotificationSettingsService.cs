using KarraMatcher.Application.Abstractions.Persistence;

namespace KarraMatcher.Application.Features.Push;

/// <summary>
/// Kontots globala notisinställning (`#332`-uppföljning). En enda på/av på kontot, i stället
/// för den tidigare per-lag/per-typ-modellen (`#65`/`#200`).
/// </summary>
public sealed class NotificationSettingsService(IAccountRepository accounts)
{
    /// <summary>Kontots på/av. Null när kontot inte finns.</summary>
    public async Task<NotificationSettingsDto?> GetAsync(
        Guid accountId,
        CancellationToken cancellationToken)
    {
        var account = await accounts.FindByIdAsync(accountId, cancellationToken).ConfigureAwait(false);

        return account is null ? null : new NotificationSettingsDto(account.NotificationsEnabled);
    }

    /// <summary>Sätter kontots på/av. Null när kontot inte finns.</summary>
    public async Task<NotificationSettingsDto?> SetAsync(
        Guid accountId,
        bool enabled,
        CancellationToken cancellationToken)
    {
        var account = await accounts.FindByIdAsync(accountId, cancellationToken).ConfigureAwait(false);

        if (account is null)
        {
            return null;
        }

        account.NotificationsEnabled = enabled;
        await accounts.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return new NotificationSettingsDto(enabled);
    }
}
