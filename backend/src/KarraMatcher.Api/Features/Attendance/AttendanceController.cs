using System.Security.Claims;

using KarraMatcher.Api.Features.Auth;
using KarraMatcher.Application.Abstractions.Messaging;
using KarraMatcher.Application.Features.Attendance;
using KarraMatcher.Domain.Attendance;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.JsonWebTokens;

namespace KarraMatcher.Api.Features.Attendance;

/// <summary>
/// Den vuxnas närvarosvar (`#57`, §KM.7).
///
/// <h3>Vem som helst med konto, men aldrig ett barn</h3>
///
/// <para>
/// En inloggad vuxen svarar för sin familj — vi har ingen förälder–lag-koppling, och får
/// inte införa en som pekar ut barn (§KM.1). Samma öppenhet som samåkningen: den som har
/// konto får delta. Svaret är en status och ett antal, aldrig ett namn.
/// </para>
///
/// <h3>Bakom grinden</h3>
///
/// <para>
/// <see cref="RequireAttendanceEnabledAttribute"/> läser matchen ur adressen och svarar
/// <c>404</c> när kallelsen är avslagen för laget — funktionen är osynlig tills klubben slår
/// på den (§KM.7).
/// </para>
/// </summary>
[ApiController]
[Route("api/v1/matches/{matchId:guid}/attendance")]
[Produces("application/json")]
[Authorize(Policy = AuthorizationPolicies.MemberOfEvent)]
[RequireAttendanceEnabled]
public sealed class AttendanceController(
    ICommandDispatcher commands,
    IQueryDispatcher queries) : ControllerBase
{
    /// <summary>Mitt eget läge: är kallelsen öppen, när är avspark, vad svarade jag.</summary>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetState(Guid matchId, CancellationToken cancellationToken)
    {
        var actor = ActorId();

        if (actor is null)
        {
            return Unauthenticated();
        }

        var state = await queries
            .SendAsync(new GetAttendanceStateQuery(matchId, actor.Value), cancellationToken)
            .ConfigureAwait(false);

        return state is null ? NotFoundForMatch() : Ok(state);
    }

    /// <summary>Sparar eller ändrar mitt svar. Går att ändra ända fram till avspark.</summary>
    [HttpPut("response")]
    [RequireCsrfToken]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Submit(
        Guid matchId,
        AttendanceResponseRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var actor = ActorId();

        if (actor is null)
        {
            return Unauthenticated();
        }

        var outcome = await commands
            .SendAsync(
                new SubmitAttendanceResponseCommand(matchId, actor.Value, request.Status, request.Count),
                cancellationToken)
            .ConfigureAwait(false);

        return outcome switch
        {
            SubmitResponseOutcome.Saved => NoContent(),

            SubmitResponseOutcome.NotCalled => Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "Ingen kallelse än",
                detail: "Tränaren har inte kallat till matchen än."),

            SubmitResponseOutcome.Closed => Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "Matchen har börjat",
                detail: "Det går inte att svara på en match som redan spelats."),

            _ => NotFoundForMatch(),
        };
    }

    private ObjectResult NotFoundForMatch() => Problem(
        statusCode: StatusCodes.Status404NotFound,
        title: "Matchen finns inte",
        detail: "Kontrollera länken — matchen kan ha tagits bort.");

    private ObjectResult Unauthenticated() => Problem(
        statusCode: StatusCodes.Status401Unauthorized,
        title: "Sessionen gäller inte längre",
        detail: "Logga in igen.");

    private Guid? ActorId()
    {
        var raw = User.FindFirstValue(JwtRegisteredClaimNames.Sub)
            ?? User.FindFirstValue(ClaimTypes.NameIdentifier);

        return Guid.TryParse(raw, out var id) ? id : null;
    }
}

/// <summary>
/// Det den vuxna fyller i: status och antal.
///
/// <para>
/// Inget barn, inget namn. Antalet prövas mot en rimlig familj, 0–4, server-side (§KM.7).
/// "Kan inte" tvingas till noll oavsett vad som skickas.
/// </para>
/// </summary>
public sealed record AttendanceResponseRequest(AttendanceStatus Status, int Count);
