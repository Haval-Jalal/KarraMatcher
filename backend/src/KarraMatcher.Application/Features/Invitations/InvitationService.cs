using System.Security.Cryptography;

using KarraMatcher.Application.Abstractions.Audit;
using KarraMatcher.Application.Abstractions.Email;
using KarraMatcher.Application.Abstractions.Persistence;
using KarraMatcher.Application.Features.Administration;
using KarraMatcher.Application.Features.Auth;
using KarraMatcher.Domain.Audit;
using KarraMatcher.Domain.Invitations;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace KarraMatcher.Application.Features.Invitations;

/// <summary>
/// Inbjudningarnas livscykel (`#193`, §KM.3): skapa, förhandsvisa, acceptera, återkalla.
///
/// <para>
/// Token behandlas som en inloggningskod: 256 bitar slump, lagras bara som hash, går ut, och
/// kan användas en gång. En accepterad inbjudan är förälderns medlemskap — det finns ingen
/// separat medlemstabell. Länken är bunden till adressen den skickades till: bara den som
/// loggar in som just den adressen kan acceptera, så en vidarebefordrad länk inte hjälper
/// fel person in. Aldrig e-post eller token i loggar (§KM.10).
/// </para>
/// </summary>
public sealed partial class InvitationService(
    IInvitationRepository invitations,
    IAuditLog audit,
    IEmailSender email,
    IOptions<AuthOptions> options,
    TimeProvider clock,
    ILogger<InvitationService> logger)
{
    public async Task<AdminResult<InvitationCreatedDto>> CreateAsync(
        Guid ageGroupId,
        string recipient,
        Guid? teamId,
        Guid actorAccountId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(recipient);

        var normalized = recipient.Trim().ToLowerInvariant();

        if (!await invitations.TruppExistsAsync(ageGroupId, cancellationToken).ConfigureAwait(false))
        {
            return AdminResults.NotFound<InvitationCreatedDto>();
        }

        if (teamId is not null
            && !await invitations.TeamInTruppAsync(teamId.Value, ageGroupId, cancellationToken)
                .ConfigureAwait(false))
        {
            return AdminResults.ReferenceMissing<InvitationCreatedDto>();
        }

        var now = clock.GetUtcNow().UtcDateTime;
        var rawToken = NewToken();

        var invitation = new Invitation
        {
            Id = Guid.NewGuid(),
            AgeGroupId = ageGroupId,
            TeamId = teamId,
            Email = normalized,
            TokenHash = SessionIssuer.Hash(rawToken),
            CreatedByAccountId = actorAccountId,
            CreatedUtc = now,
            ExpiresUtc = now.Add(options.Value.InvitationLifetime),
            Status = InvitationStatus.Pending,
        };

        await invitations.AddAsync(invitation, cancellationToken).ConfigureAwait(false);
        await audit.RecordAsync(
            AuditActions.InvitationCreated, actorAccountId, cancellationToken, invitation.Id)
            .ConfigureAwait(false);
        await invitations.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        var acceptUrl = $"{options.Value.AppBaseUrl.TrimEnd('/')}/inbjudan/{rawToken}";

        // Mejlet kastar aldrig (kontraktet på IEmailSender): en leveransmiss får inte fälla
        // skapandet, och admin har länken i svaret att skicka på annat sätt.
        await email.SendAsync(
            normalized,
            "Inbjudan till Truppen",
            $"""
            Hej!

            Du har blivit inbjuden till Truppen. Öppna länken för att gå med:

            {acceptUrl}

            Länken gäller en gång och slutar fungera efter ett tag. Har du inte väntat dig
            det här mejlet kan du strunta i det.
            """,
            cancellationToken).ConfigureAwait(false);

        return AdminResults.Ok(new InvitationCreatedDto(invitation.ToDto(), acceptUrl));
    }

    public async Task<InvitationPreviewDto?> PreviewAsync(
        string rawToken, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(rawToken);

        var invitation = await invitations
            .FindByTokenHashAsync(SessionIssuer.Hash(rawToken), cancellationToken)
            .ConfigureAwait(false);

        if (invitation is null)
        {
            return null;
        }

        var now = clock.GetUtcNow().UtcDateTime;

        return new InvitationPreviewDto(
            invitation.IsPending(now),
            invitation.AgeGroup?.Name ?? string.Empty,
            invitation.Team?.Name,
            invitation.Email);
    }

    public async Task<InvitationAcceptResult> AcceptAsync(
        string rawToken, Guid accountId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(rawToken);

        var invitation = await invitations
            .FindByTokenHashAsync(SessionIssuer.Hash(rawToken), cancellationToken)
            .ConfigureAwait(false);

        if (invitation is null)
        {
            return new InvitationAcceptResult(InvitationAcceptOutcome.NotFound, null);
        }

        // Redan använd eller återkallad: neka, och logga på id-nivå (§KM.10) — en accepterad
        // token som dyker upp igen är antingen en dubbelklick eller en läckt länk.
        if (invitation.Status != InvitationStatus.Pending)
        {
            LogInvitationReuse(logger, invitation.Id);
            return new InvitationAcceptResult(InvitationAcceptOutcome.AlreadyUsed, null);
        }

        var now = clock.GetUtcNow().UtcDateTime;

        if (invitation.ExpiresUtc <= now)
        {
            return new InvitationAcceptResult(InvitationAcceptOutcome.Expired, null);
        }

        var account = await invitations.FindAccountAsync(accountId, cancellationToken)
            .ConfigureAwait(false);

        // Bunden till adressen: den inloggade måste vara den inbjudan gäller.
        if (account is null
            || !string.Equals(account.Email, invitation.Email, StringComparison.Ordinal))
        {
            return new InvitationAcceptResult(InvitationAcceptOutcome.EmailMismatch, null);
        }

        invitation.Status = InvitationStatus.Accepted;
        invitation.AcceptedByAccountId = accountId;
        invitation.AcceptedUtc = now;

        await audit.RecordAsync(
            AuditActions.InvitationAccepted, accountId, cancellationToken, invitation.Id)
            .ConfigureAwait(false);
        await invitations.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return new InvitationAcceptResult(
            InvitationAcceptOutcome.Accepted, invitation.AgeGroup?.Name);
    }

    public async Task<AdminOutcome> RevokeAsync(
        Guid ageGroupId, Guid id, Guid actorAccountId, CancellationToken cancellationToken)
    {
        var invitation = await invitations.FindByIdAsync(id, cancellationToken).ConfigureAwait(false);

        // Objektnivå: inbjudan måste höra till truppen i adressen (som policyn gav åtkomst
        // till). Annars kunde en admin för en trupp återkalla en annans inbjudan.
        if (invitation is null || invitation.AgeGroupId != ageGroupId)
        {
            return AdminOutcome.NotFound;
        }

        // En accepterad inbjudan är ett medlemskap och återkallas inte här — det vore att ta
        // bort en förälders åtkomst via fel dörr.
        if (invitation.Status != InvitationStatus.Pending)
        {
            return AdminOutcome.Conflict;
        }

        invitation.Status = InvitationStatus.Revoked;

        await audit.RecordAsync(
            AuditActions.InvitationRevoked, actorAccountId, cancellationToken, invitation.Id)
            .ConfigureAwait(false);
        await invitations.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return AdminOutcome.Success;
    }

    /// <summary>256 bitar slump som base64url — samma sort som en refresh-token.</summary>
    private static string NewToken() =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Inbjudan {InvitationId} presenterades igen efter att den redan använts.")]
    private static partial void LogInvitationReuse(ILogger logger, Guid invitationId);
}
