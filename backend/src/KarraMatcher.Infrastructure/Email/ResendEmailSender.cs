using System.Net.Http.Json;

using KarraMatcher.Application.Abstractions.Email;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace KarraMatcher.Infrastructure.Email;

/// <summary>
/// Skickar mejl via Resend.
///
/// <para>
/// Ett leveransfel kastar inte vidare. Inloggningen får inte falla för att en leverantör
/// krånglar — och den som begärt koden ska få samma svar oavsett, annars går det att
/// mäta skillnaden mellan en adress som gick att skicka till och en som inte gjorde det.
/// </para>
/// </summary>
internal sealed partial class ResendEmailSender(
    HttpClient http,
    IOptions<EmailOptions> options,
    ILogger<ResendEmailSender> logger) : IEmailSender
{
    public async Task SendAsync(
        string recipient,
        string subject,
        string body,
        CancellationToken cancellationToken)
    {
        try
        {
            var response = await http.PostAsJsonAsync(
                "emails",
                new
                {
                    from = options.Value.From,
                    to = new[] { recipient },
                    subject,
                    text = body,
                },
                cancellationToken).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                // Statuskoden loggas, aldrig adressen och aldrig koden (§KM.10).
                LogDeliveryFailed(logger, (int)response.StatusCode);
            }
        }
        catch (HttpRequestException ex)
        {
            LogDeliveryError(logger, ex);
        }
        catch (TaskCanceledException ex)
        {
            LogDeliveryError(logger, ex);
        }
    }

    [LoggerMessage(
        EventId = 2001,
        Level = LogLevel.Error,
        Message = "Mejlleverantoren svarade {StatusCode}. Inloggningskoden nadde inte fram.")]
    private static partial void LogDeliveryFailed(ILogger logger, int statusCode);

    [LoggerMessage(
        EventId = 2002,
        Level = LogLevel.Error,
        Message = "Kunde inte na mejlleverantoren. Inloggningskoden nadde inte fram.")]
    private static partial void LogDeliveryError(ILogger logger, Exception exception);
}
