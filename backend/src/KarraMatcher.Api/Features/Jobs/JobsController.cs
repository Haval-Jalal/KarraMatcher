using System.Security.Cryptography;
using System.Text;

using KarraMatcher.Application.Abstractions.Messaging;
using KarraMatcher.Application.Features.Jobs;

using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace KarraMatcher.Api.Features.Jobs;

/// <summary>
/// De schemalagda jobbens endpoints (`#64`, §KM.11).
///
/// <h3>Skyddad av en delad hemlighet, inte av inloggning</h3>
///
/// <para>
/// Anroparen är Vercels cron, inte en människa — det finns ingen session. I stället bär
/// anropet en delad hemlighet i <c>Authorization: Bearer</c>, som jämförs mot
/// <see cref="JobOptions.Secret"/> med konstant tid, så att en angripare inte kan gissa den
/// tecken för tecken på svarstiden. Är hemligheten inte satt avvisas varje anrop.
/// </para>
///
/// <para>
/// Undantaget från "allt som skriver kräver inloggning" (§KM.3) är namngivet i
/// <c>GuestAccessTests</c>: den här skrivningen skyddas av hemligheten i stället för av en
/// token, precis som inloggningens egna endpoints skyddas av något annat än en session.
/// </para>
/// </summary>
[ApiController]
[Route("api/v1/jobs")]
[Produces("application/json")]
public sealed class JobsController(
    ICommandDispatcher commands,
    IOptions<JobOptions> options) : ControllerBase
{
    /// <summary>Skickar kvällspåminnelsen om morgondagens matcher.</summary>
    [HttpPost("match-reminders")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> MatchReminders(CancellationToken cancellationToken)
    {
        if (!IsAuthorized())
        {
            return Problem(
                statusCode: StatusCodes.Status401Unauthorized,
                title: "Obehörig",
                detail: "Jobbet kräver en giltig hemlighet.");
        }

        var reminded = await commands
            .SendAsync(new SendMatchRemindersCommand(), cancellationToken)
            .ConfigureAwait(false);

        return Ok(new JobResult(reminded));
    }

    private bool IsAuthorized()
    {
        var secret = options.Value.Secret;

        // Tom hemlighet stänger jobbet. Ett jobb utan skydd vore en väg för vem som helst att
        // avfyra notiser till hundra föräldrar.
        if (string.IsNullOrEmpty(secret))
        {
            return false;
        }

        const string prefix = "Bearer ";
        var header = Request.Headers.Authorization.ToString();

        if (!header.StartsWith(prefix, StringComparison.Ordinal))
        {
            return false;
        }

        // FixedTimeEquals: konstant tid, och falskt utan att kasta när längderna skiljer sig.
        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(header[prefix.Length..]),
            Encoding.UTF8.GetBytes(secret));
    }
}

/// <summary>Antalet matcher som påmindes om. Aldrig vilka lag eller föräldrar.</summary>
public sealed record JobResult(int Reminded);
