using KarraMatcher.Application.Abstractions.Email;

using Microsoft.Extensions.Logging;

namespace KarraMatcher.Infrastructure.Email;

/// <summary>
/// Skriver mejlet i konsolen i stället för att skicka det.
///
/// <para>
/// <b>Registreras enbart i utvecklingsmiljö.</b> Utan en leverantör går det annars inte
/// att logga in lokalt över huvud taget, eftersom koden bara finns i mejlet och lagras
/// hashad. Saknas nyckeln i drift faller uppstarten i stället — se
/// <c>DependencyInjection</c>. Att tyst låta bli att skicka inloggningskoder vore det
/// sämsta av alla utfall: allt ser ut att fungera, och ingen kommer in.
/// </para>
///
/// <para>
/// Här loggas alltså både adress och kod, vilket §KM.10 annars förbjuder. Det är
/// försvarbart uteslutande därför att mottagaren är utvecklarens egen maskin och
/// alternativet är att flödet inte går att pröva. Ett test kontrollerar att den här
/// klassen aldrig väljs utanför utveckling.
/// </para>
/// </summary>
internal sealed partial class DevelopmentEmailSender(ILogger<DevelopmentEmailSender> logger)
    : IEmailSender
{
    public Task SendAsync(
        string recipient,
        string subject,
        string body,
        CancellationToken cancellationToken)
    {
        LogEmail(logger, recipient, subject, body);

        return Task.CompletedTask;
    }

    [LoggerMessage(
        EventId = 2100,
        Level = LogLevel.Warning,
        Message = "MEJL SKICKAS INTE (utvecklingslage). Till: {Recipient} | {Subject}\n{Body}")]
    private static partial void LogEmail(
        ILogger logger,
        string recipient,
        string subject,
        string body);
}
