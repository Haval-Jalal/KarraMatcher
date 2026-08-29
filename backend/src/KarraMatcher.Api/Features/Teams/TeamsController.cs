using KarraMatcher.Api.Caching;
using KarraMatcher.Application.Abstractions.Messaging;
using KarraMatcher.Application.Features.Teams;
using KarraMatcher.Application.Features.Teams.GetTeamMatches;
using KarraMatcher.Application.Features.Teams.GetTeams;

using Microsoft.AspNetCore.Mvc;

namespace KarraMatcher.Api.Features.Teams;

/// <summary>
/// Lagen och deras matcher. Appens mest anropade yta.
///
/// <para>
/// Publik och oautentiserad (§KM.3 och §KM.0 A4): en förälder som bara vill se matchtiden
/// ska aldrig mötas av en inloggning. Svaren innehåller därför inga personuppgifter alls
/// och är cachebara utan att någon användares data blandas in.
/// </para>
/// </summary>
[ApiController]
[Route("api/v1/teams")]
[Produces("application/json")]
public sealed class TeamsController(IQueryDispatcher dispatcher) : ControllerBase
{
    /// <summary>Alla lag, för lagväljaren.</summary>
    [HttpGet]
    [EdgeCache(EdgeCacheProfile.Reference)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<TeamDto>>> GetTeams(
        CancellationToken cancellationToken)
    {
        var teams = await dispatcher
            .SendAsync(new GetTeamsQuery(), cancellationToken)
            .ConfigureAwait(false);

        return Ok(teams);
    }

    /// <summary>Ett lags matcher, sorterade på avspark.</summary>
    [HttpGet("{slug}/matches")]
    [EdgeCache(EdgeCacheProfile.Schedule)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TeamMatchesDto>> GetTeamMatches(
        string slug,
        CancellationToken cancellationToken)
    {
        var result = await dispatcher
            .SendAsync(new GetTeamMatchesQuery(slug), cancellationToken)
            .ConfigureAwait(false);

        // Ett okänt lag är inte ett fel i systemet utan en felaktig länk. 404 med
        // ProblemDetails, aldrig ett tomt schema som ser ut som en avslutad säsong.
        return result is null
            ? Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: "Laget finns inte",
                detail: "Kontrollera länken — laget kan ha bytt namn.")
            : Ok(result);
    }
}
