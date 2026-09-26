using KarraMatcher.Application.Abstractions.Messaging;
using KarraMatcher.Application.Abstractions.Persistence;
using KarraMatcher.Application.Features.Auth;

namespace KarraMatcher.Application.Features.Passkeys;

// ---- Registrering (inloggad) --------------------------------------------------------------

/// <summary>Startar registrering av en passkey för det inloggade kontot. Returnerar options-JSON.</summary>
public sealed record BeginPasskeyRegistrationQuery(Guid AccountId) : IQuery<string>;

/// <summary>Slutför registreringen: verifierar svaret och sparar passkey:n.</summary>
public sealed record CompletePasskeyRegistrationCommand(
    Guid AccountId,
    string AttestationJson,
    string? DeviceLabel) : ICommand<PasskeyRegistrationOutcome>;

// ---- Inloggning (anonym) ------------------------------------------------------------------

/// <summary>Startar en usernameless passkey-inloggning. Returnerar utmaningens id och options.</summary>
public sealed record BeginPasskeyLoginQuery : IQuery<PasskeyLoginChallenge>;

/// <summary>Slutför inloggningen: verifierar svaret och utfärdar en session, eller null.</summary>
public sealed record CompletePasskeyLoginCommand(string ChallengeId, string AssertionJson)
    : ICommand<SessionTokens?>;

// ---- Hantera sina passkeys (inloggad) -----------------------------------------------------

/// <summary>Kontots passkeys.</summary>
public sealed record ListPasskeysQuery(Guid AccountId) : IQuery<IReadOnlyList<PasskeyDto>>;

/// <summary>Tar bort en av kontots egna passkeys. Objektnivå-auktorisering: bara sin egen.</summary>
public sealed record RemovePasskeyCommand(Guid AccountId, Guid PasskeyId) : ICommand<bool>;

// ---- Handlers -----------------------------------------------------------------------------

internal sealed class BeginPasskeyRegistrationQueryHandler(IPasskeyCeremony ceremony)
    : IQueryHandler<BeginPasskeyRegistrationQuery, string>
{
    public async Task<string> HandleAsync(
        BeginPasskeyRegistrationQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        return await ceremony.BeginRegistrationAsync(query.AccountId, cancellationToken)
            .ConfigureAwait(false);
    }
}

internal sealed class CompletePasskeyRegistrationCommandHandler(IPasskeyCeremony ceremony)
    : ICommandHandler<CompletePasskeyRegistrationCommand, PasskeyRegistrationOutcome>
{
    public async Task<PasskeyRegistrationOutcome> HandleAsync(
        CompletePasskeyRegistrationCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        return await ceremony
            .CompleteRegistrationAsync(
                command.AccountId, command.AttestationJson, command.DeviceLabel, cancellationToken)
            .ConfigureAwait(false);
    }
}

internal sealed class BeginPasskeyLoginQueryHandler(IPasskeyCeremony ceremony)
    : IQueryHandler<BeginPasskeyLoginQuery, PasskeyLoginChallenge>
{
    public async Task<PasskeyLoginChallenge> HandleAsync(
        BeginPasskeyLoginQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        return await ceremony.BeginLoginAsync(cancellationToken).ConfigureAwait(false);
    }
}

internal sealed class CompletePasskeyLoginCommandHandler(
    IPasskeyCeremony ceremony,
    IAccountRepository accounts,
    SessionIssuer sessions) : ICommandHandler<CompletePasskeyLoginCommand, SessionTokens?>
{
    public async Task<SessionTokens?> HandleAsync(
        CompletePasskeyLoginCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var accountId = await ceremony
            .CompleteLoginAsync(command.ChallengeId, command.AssertionJson, cancellationToken)
            .ConfigureAwait(false);

        if (accountId is null)
        {
            return null;
        }

        var account = await accounts.FindByIdAsync(accountId.Value, cancellationToken)
            .ConfigureAwait(false);

        // Kontot kan ha raderats mellan att passkey:n verifierades och nu — då finns ingen session
        // att utfärda (kaskaden tog passkey:n med sig, men vi läser den defensivt ändå).
        if (account is null)
        {
            return null;
        }

        return await sessions.IssueAsync(account, cancellationToken).ConfigureAwait(false);
    }
}

internal sealed class ListPasskeysQueryHandler(IPasskeyRepository passkeys)
    : IQueryHandler<ListPasskeysQuery, IReadOnlyList<PasskeyDto>>
{
    public async Task<IReadOnlyList<PasskeyDto>> HandleAsync(
        ListPasskeysQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var rows = await passkeys.ListForAccountAsync(query.AccountId, cancellationToken)
            .ConfigureAwait(false);

        return rows
            .Select(p => new PasskeyDto(
                p.Id,
                p.DeviceLabel,
                new DateTimeOffset(p.CreatedUtc, TimeSpan.Zero),
                p.LastUsedUtc is { } used ? new DateTimeOffset(used, TimeSpan.Zero) : null))
            .ToList();
    }
}

internal sealed class RemovePasskeyCommandHandler(IPasskeyRepository passkeys)
    : ICommandHandler<RemovePasskeyCommand, bool>
{
    public async Task<bool> HandleAsync(
        RemovePasskeyCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var passkey = await passkeys
            .FindForAccountAsync(command.AccountId, command.PasskeyId, cancellationToken)
            .ConfigureAwait(false);

        if (passkey is null)
        {
            return false;
        }

        passkeys.Remove(passkey);
        await passkeys.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return true;
    }
}
