using System.Security.Claims;

using KarraMatcher.Application.Features.Administration;

using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.JsonWebTokens;

namespace KarraMatcher.Api.Features.Administration;

/// <summary>
/// Delad grund för superadmin-controllerna (§KM.3, `#192`).
///
/// <para>
/// Samlar två saker som annars upprepats i fem controllers: att läsa ut vem som agerar
/// (för audit) och att översätta ett <see cref="AdminOutcome"/> till rätt statuskod. Utan
/// den gemensamma översättningen driver felkoderna isär mellan resurserna.
/// </para>
/// </summary>
public abstract class AdminControllerBase : ControllerBase
{
    /// <summary>Kontot bakom anropet, ur token — för audit-loggen (§KM.10).</summary>
    protected Guid? ActorId()
    {
        var raw = User.FindFirstValue(JwtRegisteredClaimNames.Sub)
            ?? User.FindFirstValue(ClaimTypes.NameIdentifier);

        return Guid.TryParse(raw, out var id) ? id : null;
    }

    protected ObjectResult Unauthenticated() => Problem(
        statusCode: StatusCodes.Status401Unauthorized,
        title: "Sessionen gäller inte längre",
        detail: "Logga in igen.");

    /// <summary>Översätter ett utfall med värde till ett svar; anroparen väljer lyckat-svaret.</summary>
    protected IActionResult Respond<T>(AdminResult<T> result, Func<T, IActionResult> onSuccess)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(onSuccess);

        return result.Outcome switch
        {
            AdminOutcome.Success => onSuccess(result.Value!),
            _ => Failure(result.Outcome),
        };
    }

    /// <summary>Översätter ett utfall utan värde (t.ex. återkallande).</summary>
    protected IActionResult Respond(AdminOutcome outcome, IActionResult onSuccess)
    {
        ArgumentNullException.ThrowIfNull(onSuccess);

        return outcome == AdminOutcome.Success ? onSuccess : Failure(outcome);
    }

    private ObjectResult Failure(AdminOutcome outcome) => outcome switch
    {
        AdminOutcome.NotFound => Problem(
            statusCode: StatusCodes.Status404NotFound,
            title: "Finns inte",
            detail: "Kontrollera länken — posten kan ha tagits bort."),
        AdminOutcome.Conflict => Problem(
            statusCode: StatusCodes.Status409Conflict,
            title: "Upptaget",
            detail: "Namnet eller identifieraren används redan. Välj en annan."),
        AdminOutcome.ReferenceMissing => Problem(
            statusCode: StatusCodes.Status400BadRequest,
            title: "Referens saknas",
            detail: "Kontrollera att klubben, sporten eller kontot finns."),
        _ => Problem(statusCode: StatusCodes.Status500InternalServerError, title: "Okänt fel"),
    };
}
