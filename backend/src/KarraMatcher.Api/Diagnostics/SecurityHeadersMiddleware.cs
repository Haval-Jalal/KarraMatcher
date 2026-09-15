namespace KarraMatcher.Api.Diagnostics;

/// <summary>
/// Sätter säkerhetsheaders på varje svar från API:t (säkerhetschecklistan 5.1, 5.2, 4.2).
///
/// <h3>Två lager, två ansvar</h3>
///
/// <para>
/// Den <em>sidan</em> en förälder laddar serveras av Vercel, och dess CSP och ramskydd bor
/// därför i <c>frontend/vercel.json</c>. Det här API:t svarar bara med JSON — men ett
/// JSON-svar kan ändå ramas in, sniffas till fel MIME-typ eller läcka en referer. Headrarna
/// här stänger de dörrarna för backend-svaren, oberoende av vad edscachen råkar göra.
/// </para>
///
/// <h3>Varför de sätts i <c>OnStarting</c></h3>
///
/// <para>
/// Vid ett ohanterat fel nollställer felhanteraren svaret. Sätts headrarna nu i stället för
/// strax innan svaret skickas skulle de raderas på precis de svar som felhanteraren skriver
/// — samma skäl som correlation-id-headern (se <see cref="CorrelationIdMiddleware"/>).
/// </para>
///
/// <h3>HSTS bara över HTTPS</h3>
///
/// <para>
/// <c>Strict-Transport-Security</c> sätts bara när requesten faktiskt kom över HTTPS
/// (avläst ur <c>X-Forwarded-Proto</c> som Render sätter). Att skicka den över klartext på
/// <c>localhost</c> under utveckling skulle tvinga webbläsaren att låsa <c>localhost</c> till
/// HTTPS — ett självförvållat fel som är obehagligt att rensa bort.
/// </para>
/// </summary>
public sealed class SecurityHeadersMiddleware(RequestDelegate next)
{
    /// <summary>Två år, med underdomäner. Långt nog för att en preload ska vara meningsfull.</summary>
    private const string HstsValue = "max-age=63072000; includeSubDomains";

    /// <summary>
    /// API:t behöver ingen resurs och ska aldrig ramas in. En så snäv CSP kan inte gå sönder
    /// för ett JSON-svar, och stänger ändå av inramning och resursladdning för den som
    /// råkar öppna en endpoint direkt i en flik.
    /// </summary>
    private const string ApiCsp = "default-src 'none'; frame-ancestors 'none'";

    public async Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var isHttps = context.Request.IsHttps;

        context.Response.OnStarting(static state =>
        {
            var (ctx, https) = ((HttpContext, bool))state;
            var headers = ctx.Response.Headers;

            headers["X-Content-Type-Options"] = "nosniff";
            headers["X-Frame-Options"] = "DENY";
            headers["Referrer-Policy"] = "no-referrer";
            headers["Content-Security-Policy"] = ApiCsp;

            if (https)
            {
                headers["Strict-Transport-Security"] = HstsValue;
            }

            return Task.CompletedTask;
        }, (context, isHttps));

        await next(context);
    }
}
