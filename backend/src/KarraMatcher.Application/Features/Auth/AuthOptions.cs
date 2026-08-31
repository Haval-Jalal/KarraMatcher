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
}
