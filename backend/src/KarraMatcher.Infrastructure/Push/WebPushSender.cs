using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text.Json;

using KarraMatcher.Application.Abstractions.Push;
using KarraMatcher.Application.Features.Push;

using Microsoft.Extensions.Options;

namespace KarraMatcher.Infrastructure.Push;

/// <summary>
/// Skickar en notis till en enskild prenumeration (`#61`).
///
/// <h3>Svaret avgör vad som händer med raden</h3>
///
/// <para>
/// <c>404</c> och <c>410</c> betyder att prenumerationen är borta för gott — webbläsaren är
/// avinstallerad eller lagringen rensad. Då ska raden bort, inte försökas igen: en död
/// prenumeration som ligger kvar är både ett bortkastat anrop per utskick och en
/// personuppgift utan ändamål (§KM.10).
/// </para>
///
/// <para>
/// <c>5xx</c> och nätverksfel är däremot tillfälliga och förtjänar ett nytt försök. Allt
/// annat — <c>400</c>, <c>401</c>, <c>403</c>, <c>413</c> — är fel i <em>vårt</em> anrop,
/// och att försöka igen med samma innehåll ger samma svar.
/// </para>
///
/// <h3>Nyttolasten sätts ihop här</h3>
///
/// <para>
/// Service workern läser <c>title</c>, <c>body</c> och <c>url</c>. Formen hålls minimal med
/// flit: allt som skickas syns på en låsskärm, och det som inte behövs där ska inte skickas
/// alls (§KM.1, §KM.2).
/// </para>
/// </summary>
internal sealed class WebPushSender(
    HttpClient http,
    IOptions<PushOptions> options,
    TimeProvider clock) : IPushSender
{
    /// <summary>
    /// Hur länge push-tjänsten får hålla kvar notisen om telefonen är avstängd.
    ///
    /// <para>
    /// Ett dygn. En notis om att lördagens match är inställd är värdelös på tisdagen — och
    /// en förälder som slår på telefonen efter en vecka ska inte mötas av gamla besked.
    /// </para>
    /// </summary>
    private const int TimeToLiveSeconds = 86400;

    public async Task<PushOutcome> SendAsync(
        PushTarget target,
        PushMessage message,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(message);

        var push = options.Value;

        if (!push.IsConfigured)
        {
            // Ingen nyckel, ingen push. Att kasta har hade gjort en avstangd funktion till
            // ett fel i loggen vid varje matchandring.
            return PushOutcome.Failed;
        }

        if (!Uri.TryCreate(target.Endpoint, UriKind.Absolute, out var endpoint))
        {
            return PushOutcome.Failed;
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint);

        try
        {
            var payload = JsonSerializer.SerializeToUtf8Bytes(new
            {
                title = message.Title,
                body = message.Body,
                url = message.Url,
            });

            var body = WebPushEncryption.Encrypt(
                WebPushEncryption.Decode(target.P256dh),
                WebPushEncryption.Decode(target.Auth),
                payload);

            request.Content = new ByteArrayContent(body);
            request.Content.Headers.ContentType = new("application/octet-stream");
            request.Content.Headers.ContentEncoding.Add("aes128gcm");

            request.Headers.TryAddWithoutValidation(
                "Authorization",
                VapidTokens.AuthorizationHeader(
                    endpoint,
                    push.Subject,
                    push.PublicKey,
                    push.PrivateKey,
                    clock.GetUtcNow()));

            request.Headers.TryAddWithoutValidation(
                "TTL",
                TimeToLiveSeconds.ToString(CultureInfo.InvariantCulture));

            using var response = await http.SendAsync(request, cancellationToken).ConfigureAwait(false);

            return Outcome(response.StatusCode);
        }
        catch (HttpRequestException)
        {
            // Natet, inte tjansten. Nasta varv far forsoka igen.
            return PushOutcome.Retry;
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return PushOutcome.Retry;
        }
        catch (CryptographicException)
        {
            /*
             * Nycklarna fran webblasaren gar inte att anvanda. Det ar inte tillfalligt --
             * samma nycklar ger samma fel nasta gang. Raden ar trasig snarare an dod, men
             * utfallet ar detsamma for utskicket.
             */
            return PushOutcome.Failed;
        }
    }

    private static PushOutcome Outcome(HttpStatusCode status) => status switch
    {
        HttpStatusCode.NotFound or HttpStatusCode.Gone => PushOutcome.Gone,
        HttpStatusCode.TooManyRequests => PushOutcome.Retry,
        >= HttpStatusCode.InternalServerError => PushOutcome.Retry,
        >= HttpStatusCode.OK and < HttpStatusCode.Ambiguous => PushOutcome.Delivered,
        _ => PushOutcome.Failed,
    };
}
