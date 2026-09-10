using FluentValidation;

using KarraMatcher.Application.Abstractions.Audit;
using KarraMatcher.Application.Abstractions.Messaging;
using KarraMatcher.Application.Abstractions.Persistence;
using KarraMatcher.Domain.Audit;

namespace KarraMatcher.Application.Features.Attendance;

/// <summary>
/// Grinden som håller kallelsen avstängd (`#56`, §KM.7).
///
/// <h3>Varför funktionen byggs och sedan stängs av</h3>
///
/// <para>
/// Kallelsen kräver att tränaren lägger upp en trupp, alltså barns förnamn på servern —
/// och det kräver i sin tur en samtyckesrutin som inte finns än (§KM.6). Funktionen byggs
/// färdig för att den ska vara byggd <em>rätt</em> när den slås på, inte för att den ska
/// användas i dag.
/// </para>
///
/// <h3>404 och inte 403</h3>
///
/// <para>
/// Ett <c>403</c> säger "funktionen finns, men inte för dig". Ett <c>404</c> säger
/// ingenting alls, och det är rätt svar för något som inte är släppt: annars går det att
/// kartlägga vilka lag som har kallelsen påslagen genom att prova sig fram.
/// </para>
///
/// <h3>Grind och filter, inte antingen eller</h3>
///
/// <para>
/// Attributet <c>[RequireAttendanceEnabled]</c> vaktar routen och svarar innan handlern
/// körs. Den här tjänsten vaktar användningsfallet. Båda behövs: ett attribut kan glömmas
/// på en ny endpoint, och en handler kan anropas från något annat än en controller. Ett
/// test fäller bygget om en närvaro-endpoint saknar attributet.
/// </para>
/// </summary>
public sealed class AttendanceGate(IAttendanceRepository attendance)
{
    /// <summary>Sant när kallelsen är påslagen för laget. Okänt lag räknas som avstängt.</summary>
    public async Task<bool> IsEnabledForTeamAsync(string slug, CancellationToken cancellationToken) =>
        await attendance.IsEnabledForTeamAsync(slug, cancellationToken).ConfigureAwait(false) is true;

    /// <summary>Samma fråga, ställd från en match. Okänd match räknas som avstängd.</summary>
    public async Task<bool> IsEnabledForMatchAsync(Guid matchId, CancellationToken cancellationToken) =>
        await attendance.IsEnabledForMatchAsync(matchId, cancellationToken).ConfigureAwait(false) is true;
}

/// <summary>Slår på eller av kallelsen för ett lag. Bara administratör (§KM.7).</summary>
public sealed record SetAttendanceEnabledCommand(string Slug, bool Enabled, Guid ActorAccountId)
    : ICommand<bool>;

internal sealed class SetAttendanceEnabledCommandValidator
    : AbstractValidator<SetAttendanceEnabledCommand>
{
    public SetAttendanceEnabledCommandValidator()
    {
        RuleFor(c => c.Slug).NotEmpty();
        RuleFor(c => c.ActorAccountId).NotEmpty();
    }
}

/// <summary>
/// Ändrar flaggan och skriver en audit-rad.
///
/// <para>
/// Att slå på kallelsen för ett lag är att börja behandla uppgifter om barn på servern.
/// Det är precis en sådan åtgärd §KM.10 kräver ska gå att härleda i efterhand: vem, vilket
/// lag, och när. Raden bär lagets slug och kontots id — aldrig ett namn.
/// </para>
/// </summary>
internal sealed class SetAttendanceEnabledCommandHandler(
    IAttendanceRepository attendance,
    IAuditLog audit) : ICommandHandler<SetAttendanceEnabledCommand, bool>
{
    public async Task<bool> HandleAsync(
        SetAttendanceEnabledCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var teamId = await attendance
            .SetEnabledAsync(command.Slug, command.Enabled, cancellationToken)
            .ConfigureAwait(false);

        if (teamId is null)
        {
            return false;
        }

        await audit.RecordAsync(
            command.Enabled ? AuditActions.AttendanceEnabled : AuditActions.AttendanceDisabled,
            command.ActorAccountId,
            cancellationToken,
            teamId.Value,
            command.Slug).ConfigureAwait(false);

        // Ett sparande for bada. Skrivs raden separat kan flaggan andras utan att det syns,
        // eller synas utan att den andrades -- och en auditlogg man inte kan lita pa ar
        // varre an ingen, for den anvands som bevis.
        await attendance.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return true;
    }
}
