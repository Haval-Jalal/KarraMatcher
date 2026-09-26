using KarraMatcher.Application.Abstractions.Audit;
using KarraMatcher.Application.Abstractions.Persistence;
using KarraMatcher.Domain.Attendance;
using KarraMatcher.Domain.Audit;
using KarraMatcher.Domain.Events;

namespace KarraMatcher.Application.Features.Cup;

/// <summary>Vad ett försök att öppna en cups anmälan slutade med (`#295`).</summary>
public enum OpenCupOutcome
{
    /// <summary>Anmälan öppnades (eller platstaket ändrades).</summary>
    Opened = 0,

    /// <summary>Händelsen finns inte eller hör till en annan trupp.</summary>
    EventNotInTrupp = 1,

    /// <summary>Händelsen är inte en cup — bara cuper har öppen anmälan.</summary>
    NotACup = 2,
}

/// <summary>Vad ett försök att anmäla ett barn till en cup slutade med (`#295`).</summary>
public enum CupSignupOutcome
{
    /// <summary>Barnet anmäldes.</summary>
    SignedUp = 0,

    /// <summary>Anmälan är inte öppnad för den här cupen (inget platstak satt).</summary>
    SignupNotOpen = 1,

    /// <summary>Platserna är slut — först till kvarn (§KM.12-stil).</summary>
    Full = 2,

    /// <summary>Händelsen är inte en cup.</summary>
    NotACup = 3,

    /// <summary>Den inloggade är inte vårdnadshavare för barnet.</summary>
    NotGuardian = 4,

    /// <summary>Barnet hör inte till cupens trupp.</summary>
    ChildNotInTrupp = 5,

    /// <summary>Barnet är redan anmält.</summary>
    AlreadySignedUp = 6,
}

/// <summary>Vad ett försök att dra tillbaka en anmälan slutade med (`#295`).</summary>
public enum CupWithdrawOutcome
{
    /// <summary>Anmälan drogs tillbaka och platsen frigjordes.</summary>
    Withdrawn = 0,

    /// <summary>Barnet var inte anmält.</summary>
    NotSignedUp = 1,

    /// <summary>Den inloggade är inte vårdnadshavare för barnet.</summary>
    NotGuardian = 2,
}

/// <summary>Ett anmält barn i sammanställningen (`#295`). Visas som "Liam J" (§KM.1).</summary>
public sealed record CupSignupChildDto(Guid ChildId, string DisplayName, string? TeamName, string? ColorHex);

/// <summary>Den inloggades eget barn i cupens trupp, och om det är anmält (`#296`).</summary>
public sealed record MyCupChildDto(Guid ChildId, string DisplayName, bool SignedUp);

/// <summary>
/// Cupens anmälningsläge: platstak, antal tagna, de anmälda barnen och — för en vårdnadshavare
/// — hens egna barn att anmäla (`#295`/`#296`).
/// </summary>
public sealed record CupSummaryDto(
    bool Open,
    int? Capacity,
    int SpotsTaken,
    int SpotsLeft,
    bool IsFull,
    IReadOnlyList<CupSignupChildDto> SignedUp,
    IReadOnlyList<MyCupChildDto> Mine);

/// <summary>
/// Cupens <b>öppna</b> anmälan (`#295`). Till skillnad från den riktade kallelsen (`#199`, där
/// tränaren väljer vilka barn som kallas) sätter tränaren här bara ett <em>platstak</em>, och
/// vilken vårdnadshavare som helst i truppen anmäler sina egna barn — först till kvarn tills
/// platserna är slut.
///
/// <para>
/// "Fullt" räknas alltid fram ur antalet Ja (§KM.12) och lagras aldrig som en flagga. Taket
/// tvingas server-side: en anmälan som skulle överskrida det avvisas, aldrig bara en dold knapp.
/// Modellen återanvänder <see cref="AttendanceCall"/> (med ett <c>Capacity</c>) och per-barn-raderna
/// i <see cref="AttendanceInvitation"/> — en anmälan är en rad med <see cref="AttendanceReply.Coming"/>.
/// </para>
/// </summary>
public sealed class CupSignupService(
    IAttendanceCallRepository calls,
    IMembershipService membership,
    IAuditLog audit)
{
    /// <summary>Tränaren öppnar (eller ändrar) cupens platstak. Kräver att händelsen är en cup.</summary>
    public async Task<OpenCupOutcome> OpenAsync(
        Guid truppId,
        Guid eventId,
        int capacity,
        Guid actorAccountId,
        CancellationToken cancellationToken)
    {
        var context = await calls.FindEventContextAsync(eventId, cancellationToken).ConfigureAwait(false);

        if (context is null || context.AgeGroupId != truppId)
        {
            return OpenCupOutcome.EventNotInTrupp;
        }

        if (context.Type != EventType.Cup)
        {
            return OpenCupOutcome.NotACup;
        }

        var call = await calls.FindCallByEventAsync(eventId, cancellationToken).ConfigureAwait(false);

        if (call is null)
        {
            call = new AttendanceCall
            {
                Id = Guid.NewGuid(),
                MatchId = eventId,
                OpenedByAccountId = actorAccountId,
                OpenedUtc = DateTime.UtcNow,
                Capacity = capacity,
            };

            await calls.AddCallAsync(call, cancellationToken).ConfigureAwait(false);

            await audit.RecordAsync(
                AuditActions.AttendanceCallOpened, actorAccountId, cancellationToken, eventId)
                .ConfigureAwait(false);
        }
        else
        {
            // Tränaren kan justera taket i efterhand. Redan anmälda barn rörs inte — sänks taket
            // under antalet anmälda är cupen bara "full" tills någon drar sig ur.
            call.Capacity = capacity;
        }

        await calls.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return OpenCupOutcome.Opened;
    }

    /// <summary>En vårdnadshavare anmäler sitt barn. Först till kvarn: full cup avvisas.</summary>
    public async Task<CupSignupOutcome> SignUpAsync(
        Guid eventId,
        Guid childId,
        Guid accountId,
        CancellationToken cancellationToken)
    {
        var context = await calls.FindEventContextAsync(eventId, cancellationToken).ConfigureAwait(false);

        if (context is null || context.Type != EventType.Cup)
        {
            return CupSignupOutcome.NotACup;
        }

        var call = await calls.FindCallByEventAsync(eventId, cancellationToken).ConfigureAwait(false);

        if (call is null || call.Capacity is null)
        {
            return CupSignupOutcome.SignupNotOpen;
        }

        // Objektnivå: bara den egna vårdnadshavaren, bara ett barn i cupens trupp (§KM.1/IDOR).
        if (!await calls.IsGuardianOfChildAsync(accountId, childId, cancellationToken).ConfigureAwait(false))
        {
            return CupSignupOutcome.NotGuardian;
        }

        var truppChildren = await calls
            .ChildIdsInTruppAsync(context.AgeGroupId, cancellationToken)
            .ConfigureAwait(false);

        if (!truppChildren.Contains(childId))
        {
            return CupSignupOutcome.ChildNotInTrupp;
        }

        var existing = await calls
            .FindInvitationAsync(call.Id, childId, cancellationToken)
            .ConfigureAwait(false);

        if (existing is not null && existing.Reply == AttendanceReply.Coming)
        {
            return CupSignupOutcome.AlreadySignedUp;
        }

        /*
         * Platsräkningen läses precis före skrivningen. Två anmälningar som samtidigt tar den
         * sista platsen kan i teorin båda släppas igenom — samma pris som samåkningens
         * platsräkning (§KM.12) betalar, och för en trupp på ett fyrtiotal barn är det försumbart.
         */
        var taken = await calls.CountComingAsync(call.Id, cancellationToken).ConfigureAwait(false);

        if (taken >= call.Capacity.Value)
        {
            return CupSignupOutcome.Full;
        }

        if (existing is null)
        {
            await calls.AddInvitationAsync(
                new AttendanceInvitation
                {
                    Id = Guid.NewGuid(),
                    CallId = call.Id,
                    ChildId = childId,
                    Reply = AttendanceReply.Coming,
                    RespondedByAccountId = accountId,
                    RespondedUtc = DateTime.UtcNow,
                },
                cancellationToken).ConfigureAwait(false);
        }
        else
        {
            existing.Reply = AttendanceReply.Coming;
            existing.RespondedByAccountId = accountId;
            existing.RespondedUtc = DateTime.UtcNow;
        }

        await calls.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return CupSignupOutcome.SignedUp;
    }

    /// <summary>Drar tillbaka en anmälan och frigör platsen.</summary>
    public async Task<CupWithdrawOutcome> WithdrawAsync(
        Guid eventId,
        Guid childId,
        Guid accountId,
        CancellationToken cancellationToken)
    {
        var call = await calls.FindCallByEventAsync(eventId, cancellationToken).ConfigureAwait(false);

        if (call is null)
        {
            return CupWithdrawOutcome.NotSignedUp;
        }

        if (!await calls.IsGuardianOfChildAsync(accountId, childId, cancellationToken).ConfigureAwait(false))
        {
            return CupWithdrawOutcome.NotGuardian;
        }

        var invitation = await calls
            .FindInvitationAsync(call.Id, childId, cancellationToken)
            .ConfigureAwait(false);

        if (invitation is null || invitation.Reply != AttendanceReply.Coming)
        {
            return CupWithdrawOutcome.NotSignedUp;
        }

        calls.RemoveInvitation(invitation);
        await calls.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return CupWithdrawOutcome.Withdrawn;
    }

    /// <summary>Cupens anmälningsläge för en truppmedlem, eller null när det inte är en cup / inte medlem.</summary>
    public async Task<CupSummaryDto?> SummaryAsync(
        Guid eventId,
        Guid accountId,
        CancellationToken cancellationToken)
    {
        var context = await calls.FindEventContextAsync(eventId, cancellationToken).ConfigureAwait(false);

        if (context is null || context.Type != EventType.Cup)
        {
            return null;
        }

        // Sammanställningen namnger barn (§KM.1), så bara truppens medlemmar ser den.
        if (!await membership
            .IsMemberOfTruppAsync(accountId, context.AgeGroupId, cancellationToken)
            .ConfigureAwait(false))
        {
            return null;
        }

        var call = await calls.FindCallByEventAsync(eventId, cancellationToken).ConfigureAwait(false);

        // Den inloggades egna barn i truppen (även oanmälda) — så en vårdnadshavare kan anmäla.
        // Guid.Empty när anmälan inte öppnats: då är inget barn anmält än.
        var mine = (await calls
            .MyCupChildrenAsync(context.AgeGroupId, accountId, call?.Id ?? Guid.Empty, cancellationToken)
            .ConfigureAwait(false))
            .Select(row => new MyCupChildDto(
                row.ChildId, $"{row.FirstName} {row.LastInitial}", row.SignedUp))
            .ToArray();

        if (call is null || call.Capacity is null)
        {
            return new CupSummaryDto(false, null, 0, 0, false, [], mine);
        }

        var rows = await calls.ListInvitationRowsAsync(call.Id, cancellationToken).ConfigureAwait(false);
        var signedUp = rows
            .Where(row => row.Reply == AttendanceReply.Coming)
            .Select(row => new CupSignupChildDto(
                row.ChildId, $"{row.FirstName} {row.LastInitial}", row.TeamName, row.ColorHex))
            .ToArray();

        var capacity = call.Capacity.Value;
        var taken = signedUp.Length;
        var spotsLeft = Math.Max(0, capacity - taken);

        return new CupSummaryDto(true, capacity, taken, spotsLeft, taken >= capacity, signedUp, mine);
    }
}
