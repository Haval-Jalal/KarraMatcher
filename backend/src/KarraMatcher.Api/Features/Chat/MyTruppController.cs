using System.Security.Claims;

using KarraMatcher.Application.Abstractions.Persistence;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.JsonWebTokens;

namespace KarraMatcher.Api.Features.Chat;

/// <summary>
/// Trupperna den inloggade är medlem av (`#201`). Låter en medlem hitta sin trupp-chatt utan
/// att vara admin — inloggning räcker, listan är bara den egna medlemmens.
/// </summary>
[ApiController]
[Route("api/v1/trupper/mina")]
[Produces("application/json")]
[Authorize]
public sealed class MyTruppController(IMembershipService membership) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<IReadOnlyList<MemberTruppDto>>> Mine(
        CancellationToken cancellationToken)
    {
        var raw = User.FindFirstValue(JwtRegisteredClaimNames.Sub)
            ?? User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (!Guid.TryParse(raw, out var accountId))
        {
            return Problem(
                statusCode: StatusCodes.Status401Unauthorized,
                title: "Sessionen gäller inte längre",
                detail: "Logga in igen.");
        }

        var trupper = await membership.MemberTrupperAsync(accountId, cancellationToken)
            .ConfigureAwait(false);

        return Ok(trupper);
    }
}
