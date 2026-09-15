using System.Security.Claims;
using KarraMatcher.Api.Diagnostics;
using KarraMatcher.Application.Abstractions.Messaging;
using KarraMatcher.Application.Features.Accounts;
using KarraMatcher.Application.Features.Auth.DeleteAccount;
using KarraMatcher.Application.Features.Auth.ExportAccount;
using KarraMatcher.Application.Features.Auth.RefreshSession;
using KarraMatcher.Application.Features.Auth.RequestLoginCode;
using KarraMatcher.Application.Features.Auth.SignOut;
using KarraMatcher.Application.Features.Auth.VerifyLoginCode;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.JsonWebTokens;

namespace KarraMatcher.Api.Features.Auth;

/// <summary>
/// Sessionens livscykel: förnya och logga ut.
///
/// <para>
/// Inloggningen finns inte här än — den kommer i <c>#29</c> och lägger till en endpoint
/// som utfärdar den första sessionen. Det som finns nu är allt <em>runt</em> den, och det
/// är den delen som är svår att göra rätt i efterhand.
/// </para>
///
/// <para>
/// Refresh-token kommer alltid ur cookien och aldrig ur en body. Det är avsiktligt: en
/// token i en body går att råka logga, hamna i en URL, eller läsas av skript på sidan.
/// </para>
/// </summary>
[ApiController]
[Route("api/v1/auth")]
[Produces("application/json")]
public sealed class AuthController(
    ICommandDispatcher dispatcher,
    IQueryDispatcher queries,
    IAntiforgery antiforgery) : ControllerBase
{
    /// <summary>
    /// Hämtar en anti-forgery-token som klienten skickar tillbaka i <c>X-CSRF-TOKEN</c>.
    /// </summary>
    /// <remarks>
    /// Behövs eftersom refresh-token ligger i en cookie: utan CSRF-skydd hade en annan
    /// webbplats kunnat få webbläsaren att förnya sessionen åt sig. <c>SameSite=Lax</c>
    /// stoppar det mesta, men checklistan 6.5 kräver båda — och Lax skyddar inte mot en
    /// underdomän som blivit kapad.
    /// </remarks>
    [HttpGet("csrf")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult GetCsrfToken()
    {
        var tokens = antiforgery.GetAndStoreTokens(HttpContext);

        return Ok(new CsrfTokenResponse(tokens.RequestToken ?? string.Empty));
    }

    /// <summary>Begär en engångskod till en mejladress.</summary>
    /// <remarks>
    /// Svarar alltid 202, oavsett om adressen är känd, om en kod skickades, eller om
    /// mejlet gick fram. Ett svar som skiljde på fallen hade gjort inloggningsrutan till
    /// en adresslista för den som frågar tillräckligt många gånger.
    /// </remarks>
    [HttpPost("request-code")]
    [RequireCsrfToken]
    [EnableRateLimiting(RateLimiting.LoginPolicy)]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    public async Task<IActionResult> RequestCode(
        RequestCodeRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        await dispatcher
            .SendAsync(new RequestLoginCodeCommand(request.Email), cancellationToken)
            .ConfigureAwait(false);

        return Accepted();
    }

    /// <summary>Verifierar koden och startar sessionen.</summary>
    [HttpPost("verify-code")]
    [RequireCsrfToken]
    [EnableRateLimiting(RateLimiting.LoginPolicy)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> VerifyCode(
        VerifyCodeRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var session = await dispatcher
            .SendAsync(new VerifyLoginCodeCommand(request.Email, request.Code), cancellationToken)
            .ConfigureAwait(false);

        if (session is null)
        {
            // Ett enda svar för fel kod, utgången kod, förbrukad kod, för många försök
            // och okänd adress. Skillnaden mellan dem är upplysande för den som provar.
            return Problem(
                statusCode: StatusCodes.Status401Unauthorized,
                title: "Koden stämmer inte",
                detail: "Kontrollera koden, eller begär en ny.");
        }

        SessionCookie.Write(Response, session.RefreshToken, session.RefreshExpiresUtc);

        return Ok(new SessionResponse(session.AccessToken, session.AccessExpiresUtc));
    }

    /// <summary>Byter refresh-cookien mot en ny access-token och en ny cookie.</summary>
    [HttpPost("refresh")]
    [RequireCsrfToken]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Refresh(CancellationToken cancellationToken)
    {
        var cookie = SessionCookie.Read(Request);

        var session = cookie is null
            ? null
            : await dispatcher
                .SendAsync(new RefreshSessionCommand(cookie), cancellationToken)
                .ConfigureAwait(false);

        if (session is null)
        {
            /*
             * Cookien rensas aven har. Gick fornyelsen inte igenom ar den vardelos, och en
             * kvarliggande cookie far klienten att forsoka igen i all evighet.
             *
             * Samma svar oavsett orsak -- okand token, utgangen, eller en familj som
             * fallit for att nagon aterandvant en token. Anroparen ska inte kunna avgora
             * vilket, eftersom skillnaden i sig ar upplysande for den som provar sig fram.
             */
            SessionCookie.Clear(Response);

            return Problem(
                statusCode: StatusCodes.Status401Unauthorized,
                title: "Sessionen gäller inte längre",
                detail: "Logga in igen.");
        }

        SessionCookie.Write(Response, session.RefreshToken, session.RefreshExpiresUtc);

        // Refresh-token följer med i cookien och aldrig i kroppen.
        return Ok(new SessionResponse(session.AccessToken, session.AccessExpiresUtc));
    }

    /// <summary>Kontots eget namn (`#154`).</summary>
    /// <remarks>
    /// Bara den inloggade får fråga efter sitt eget — det finns inget kontofält att skicka,
    /// så det går inte att fråga efter någon annans. Namnet i sig når andra bara genom
    /// samåkningen, och bara den som är inloggad (§KM.3).
    /// </remarks>
    [HttpGet("profile")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetProfile(CancellationToken cancellationToken)
    {
        var accountId = CurrentAccountId();

        if (accountId is null)
        {
            return Unauthenticated();
        }

        var profile = await queries
            .SendAsync(new GetAccountProfileQuery(accountId.Value), cancellationToken)
            .ConfigureAwait(false);

        return profile is null ? Unauthenticated() : Ok(profile);
    }

    /// <summary>Sätter eller ändrar namnet på kontot.</summary>
    [HttpPut("profile")]
    [Authorize]
    [RequireCsrfToken]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> UpdateProfile(
        AccountNameRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var accountId = CurrentAccountId();

        if (accountId is null)
        {
            return Unauthenticated();
        }

        var profile = await dispatcher
            .SendAsync(
                new UpdateAccountNameCommand(accountId.Value, request.FirstName, request.LastName),
                cancellationToken)
            .ConfigureAwait(false);

        return profile is null ? Unauthenticated() : Ok(profile);
    }

    /// <summary>Registerutdrag: allt servern har om det inloggade kontot (`#67`).</summary>
    /// <remarks>
    /// Bara den inloggade får hämta sitt eget — id:t kommer ur token, det finns inget
    /// kontofält att fråga efter någon annans med. Svaret bär personuppgifter och får
    /// därför aldrig edge-cachas; det lämnas som <c>private</c> genom att inte begära cache
    /// (§KM.11, cachning är opt-in). En säker GET behöver ingen CSRF-token.
    /// </remarks>
    [HttpGet("export")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Export(CancellationToken cancellationToken)
    {
        var accountId = CurrentAccountId();

        if (accountId is null)
        {
            return Unauthenticated();
        }

        var export = await queries
            .SendAsync(new ExportAccountQuery(accountId.Value), cancellationToken)
            .ConfigureAwait(false);

        return export is null ? Unauthenticated() : Ok(export);
    }

    /// <summary>Raderar kontot och allt servern äger om det.</summary>
    /// <remarks>
    /// Direkt, inte som en markering (§KM.6). Spelarkortet berörs inte och kan inte
    /// beröras — det har aldrig nått servern (§KM.2). Att det ligger kvar i telefonen är
    /// gränssnittets sak att förklara, och det gör det.
    /// </remarks>
    [HttpDelete("account")]
    [Authorize]
    [RequireCsrfToken]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> DeleteAccount(CancellationToken cancellationToken)
    {
        var accountId = CurrentAccountId();

        if (accountId is null)
        {
            // Token utan sub. Ska inte kunna hända -- vi utfärdar det alltid -- men att
            // radera "vem som helst" vore ett katastrofalt sätt att ha fel.
            return Problem(
                statusCode: StatusCodes.Status401Unauthorized,
                title: "Sessionen gäller inte längre",
                detail: "Logga in igen.");
        }

        await dispatcher
            .SendAsync(new DeleteAccountCommand(accountId.Value), cancellationToken)
            .ConfigureAwait(false);

        SessionCookie.Clear(Response);

        return NoContent();
    }

    private ObjectResult Unauthenticated() => Problem(
        statusCode: StatusCodes.Status401Unauthorized,
        title: "Sessionen gäller inte längre",
        detail: "Logga in igen.");

    /// <summary>
    /// Kontot som är inloggat, ur token.
    ///
    /// <para>
    /// Två anspråksnamn prövas: <c>sub</c> som vi skriver, och den längre URI:n som
    /// ramverket kan mappa den till. Vilken som syns beror på inställningar som inte hör
    /// hemma i den här metoden, och att bara läsa den ena är ett fel som ger 401 för alla.
    /// </para>
    /// </summary>
    private Guid? CurrentAccountId()
    {
        var raw = User.FindFirstValue(JwtRegisteredClaimNames.Sub)
            ?? User.FindFirstValue(ClaimTypes.NameIdentifier);

        return Guid.TryParse(raw, out var id) ? id : null;
    }

    /// <summary>Avslutar sessionen och återkallar hela dess familj.</summary>
    [HttpPost("logout")]
    [RequireCsrfToken]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Logout(CancellationToken cancellationToken)
    {
        await dispatcher
            .SendAsync(new SignOutCommand(SessionCookie.Read(Request)), cancellationToken)
            .ConfigureAwait(false);

        SessionCookie.Clear(Response);

        // Alltid 204, även utan giltig session. Att logga ut ska inte kunna användas för
        // att ta reda på om en token var giltig.
        return NoContent();
    }
}

/// <summary>Anti-forgery-token att skicka i <c>X-CSRF-TOKEN</c>.</summary>
public sealed record CsrfTokenResponse(string Token);

/// <summary>
/// Den nya sessionen så som klienten ser den.
///
/// <para>
/// Access-token lever i minnet hos klienten, aldrig i <c>localStorage</c>. Refresh-token
/// finns inte med här alls — den lämnar servern enbart som <c>httpOnly</c>-cookie.
/// </para>
/// </summary>
public sealed record SessionResponse(string AccessToken, DateTime ExpiresUtc);

/// <summary>Begäran om en engångskod.</summary>
public sealed record RequestCodeRequest(string Email);

/// <summary>Koden från mejlet, tillsammans med adressen den skickades till.</summary>
public sealed record VerifyCodeRequest(string Email, string Code);

/// <summary>
/// Namnet föräldern skriver in.
///
/// <para>
/// Förnamnet krävs, efternamnet är valfritt. Kraven prövas i valideringen av kommandot och
/// inte här, så att ingen väg in i tjänsten kan komma runt dem.
/// </para>
/// </summary>
public sealed record AccountNameRequest(string FirstName, string? LastName);
