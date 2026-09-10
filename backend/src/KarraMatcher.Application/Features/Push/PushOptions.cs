using System.ComponentModel.DataAnnotations;

namespace KarraMatcher.Application.Features.Push;

/// <summary>
/// VAPID-nycklarna som identifierar den här servern mot webbläsarnas push-tjänster
/// (`#60`, säkerhetschecklistan 7.5).
///
/// <h3>Den privata nyckeln lämnar aldrig backend</h3>
///
/// <para>
/// Den signerar de intyg som säger till Google, Apple och Mozilla att utskicket kommer
/// från oss. Kommer den ut kan vem som helst skicka notiser i appens namn till varje
/// förälder som prenumererar. Den finns därför bara i konfiguration — user-secrets lokalt,
/// miljövariabel i Render — och aldrig i kod, i en incheckad appsettings, eller i
/// frontendens bundle, som är publik i sin helhet.
/// </para>
///
/// <h3>Den publika nyckeln är däremot menad att spridas</h3>
///
/// <para>
/// Webbläsaren behöver den för att kunna prenumerera. Den serveras av API:t i stället för
/// att bakas in i frontendens bygge, så att ett nyckelbyte inte kräver en ny driftsättning
/// av frontenden — och så att de två aldrig kan glida isär.
/// </para>
///
/// <h3>Saknas nycklarna är push avstängt, inte trasigt</h3>
///
/// <para>
/// Appen ska gå att köra utan dem: kalenderfeeden är den primära kanalen och push är
/// komplementet (§KM.0 A5). Därför är fälten inte <c>[Required]</c> — men sätts <em>ett</em>
/// av dem måste alla sättas, annars är det en halv konfiguration som ser ut att fungera.
/// </para>
/// </summary>
public sealed class PushOptions : IValidatableObject
{
    public const string SectionName = "Push";

    /// <summary>Publik VAPID-nyckel, base64url. Serveras till webbläsaren.</summary>
    public string PublicKey { get; set; } = string.Empty;

    /// <summary>Privat VAPID-nyckel, base64url. Hemlighet.</summary>
    public string PrivateKey { get; set; } = string.Empty;

    /// <summary>
    /// Kontaktadress som följer med utskicken, enligt VAPID: <c>mailto:</c> eller en URL.
    ///
    /// <para>
    /// Push-tjänsterna använder den för att höra av sig när något är fel med våra utskick.
    /// Den är alltså vår egen adress och ingen användares.
    /// </para>
    /// </summary>
    [MaxLength(200)]
    public string Subject { get; set; } = "mailto:karra.matcher@example.com";

    /// <summary>Sant när push går att använda över huvud taget.</summary>
    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(PublicKey) && !string.IsNullOrWhiteSpace(PrivateKey);

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        var hasPublic = !string.IsNullOrWhiteSpace(PublicKey);
        var hasPrivate = !string.IsNullOrWhiteSpace(PrivateKey);

        if (hasPublic != hasPrivate)
        {
            yield return new ValidationResult(
                "Bade Push:PublicKey och Push:PrivateKey maste sattas, eller ingen av dem.",
                [nameof(PublicKey), nameof(PrivateKey)]);
        }

        if (IsConfigured && string.IsNullOrWhiteSpace(Subject))
        {
            yield return new ValidationResult(
                "Push:Subject kravs nar VAPID-nycklar ar satta.",
                [nameof(Subject)]);
        }
    }
}
