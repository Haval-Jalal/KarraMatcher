using System.ComponentModel.DataAnnotations;

namespace KarraMatcher.Application.Features.Auth;

/// <summary>
/// Inställningar för inloggningen. Valideras vid start (<c>ValidateOnStart</c>) —
/// en felaktig nyckel ska fälla driftsättningen, inte första inloggningen.
/// </summary>
public sealed class AuthOptions
{
    public const string SectionName = "Auth";

    /// <summary>
    /// Signeringsnyckeln. Kommer alltid ur konfiguration — user-secrets lokalt,
    /// miljövariabel i Render. Aldrig i kod och aldrig i incheckad appsettings.
    /// </summary>
    [Required]
    [MinLength(32, ErrorMessage = "Signeringsnyckeln maste vara minst 32 tecken.")]
    public string SigningKey { get; set; } = string.Empty;

    [Required]
    public string Issuer { get; set; } = "karra-matcher";

    [Required]
    public string Audience { get; set; } = "karra-matcher-app";

    /// <summary>
    /// Kort nog att en stulen access-token är nästan värdelös. Klienten förnyar tyst
    /// mot refresh-cookien, så användaren märker ingenting.
    /// </summary>
    public TimeSpan AccessTokenLifetime { get; set; } = TimeSpan.FromMinutes(15);

    /// <summary>
    /// Lång, för att appen används säsongsvis: en förälder som öppnar den en gång i
    /// månaden ska inte mötas av inloggning. Rotationen är det som gör den långa
    /// livstiden försvarbar.
    /// </summary>
    public TimeSpan RefreshTokenLifetime { get; set; } = TimeSpan.FromDays(60);

    /// <summary>
    /// Hur länge en engångskod går att använda.
    ///
    /// <para>
    /// Tio minuter är avvägt mot verkligheten: mejlet ska hinna fram, föräldern ska hinna
    /// byta app och skriva av sex siffror, och en kod som råkar bli kvar i en inkorg ska
    /// vara död långt innan någon annan läser den.
    /// </para>
    /// </summary>
    public TimeSpan LoginCodeLifetime { get; set; } = TimeSpan.FromMinutes(10);

    /// <summary>
    /// Antal felgissningar innan koden är död.
    ///
    /// <para>
    /// Det här talet är vad som gör en sexsiffrig kod försvarbar. En miljon möjliga koder
    /// och fem försök ger en chans på tvåhundratusen per kod — och koden lever i tio
    /// minuter. Utan spärren hade siffrorna varit meningslösa.
    /// </para>
    /// </summary>
    public int MaxLoginCodeAttempts { get; set; } = 5;

    /// <summary>
    /// Kortaste tid mellan två utskickade koder till samma adress.
    ///
    /// <para>
    /// Skyddar en förälders inkorg från att fyllas av någon som upprepar begäran. Svaret
    /// utåt är detsamma oavsett — den som frågar ska inte kunna mäta skillnaden.
    /// </para>
    /// </summary>
    public TimeSpan LoginCodeResendCooldown { get; set; } = TimeSpan.FromSeconds(60);
}
