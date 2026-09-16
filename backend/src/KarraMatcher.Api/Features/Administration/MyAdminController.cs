using KarraMatcher.Api.Features.Auth;
using KarraMatcher.Application.Abstractions.Messaging;
using KarraMatcher.Application.Features.Administration;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KarraMatcher.Api.Features.Administration;

/// <summary>
/// Den inloggades egen admin-yta (`#193`): trupperna hen sköter.
///
/// <para>
/// Vilken inloggad som helst får fråga — svaret är tomt för den som inte är admin för någon
/// trupp, och alla trupper för en superadmin. Det som faktiskt går att göra i en trupp
/// vaktas av <c>AdminOfTrupp</c> på respektive endpoint.
/// </para>
/// </summary>
[ApiController]
[Route("api/v1/admin/my-trupper")]
[Produces("application/json")]
[Authorize]
public sealed class MyAdminController(IQueryDispatcher queries) : AdminControllerBase
{
    /// <summary>Trupperna den inloggade är admin för.</summary>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<IReadOnlyList<TruppDto>>> List(CancellationToken cancellationToken)
    {
        var accountId = ActorId();

        if (accountId is null)
        {
            return Unauthenticated();
        }

        var trupper = await queries
            .SendAsync(
                new GetMyTrupperQuery(accountId.Value, Membership.IsSuperAdmin(User)),
                cancellationToken)
            .ConfigureAwait(false);

        return Ok(trupper);
    }
}
