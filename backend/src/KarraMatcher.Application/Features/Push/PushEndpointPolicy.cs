namespace KarraMatcher.Application.Features.Push;

/// <summary>
/// Vilka adresser en push-prenumeration får peka på (`#581`, SSRF/OWASP A10).
///
/// <para>
/// Web Push når bara ett fåtal pushtjänster. Utan en host-allowlist kunde en inloggad medlem
/// registrera en godtycklig <c>https</c>-adress och få servern att POST:a en krypterad kropp
/// <b>plus en VAPID-signerad JWT</b> dit när en lagnotis går ut — inklusive interna/metadata-
/// adresser på driftnätet. En strikt allowlist stänger det: bara de kända tjänsternas värdnamn
/// släpps igenom, och IP-literaler eller interna värdar matchar aldrig suffixen.
/// </para>
/// </summary>
public static class PushEndpointPolicy
{
    // Värdnamn (exakt) och värd-suffix (med inledande punkt) för de etablerade pushtjänsterna.
    private static readonly string[] AllowedExactHosts = ["fcm.googleapis.com"];

    private static readonly string[] AllowedHostSuffixes =
    [
        ".push.services.mozilla.com", // Firefox (updates.push.services.mozilla.com)
        ".notify.windows.com", // Edge/WNS (*.notify.windows.com)
        ".push.apple.com", // Safari (web.push.apple.com)
    ];

    /// <summary>En absolut https-adress till en känd pushtjänst — annars nej.</summary>
    public static bool IsAllowedEndpoint(string? value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var uri) && IsAllowedEndpoint(uri);

    /// <summary>En absolut https-adress till en känd pushtjänst — annars nej.</summary>
    public static bool IsAllowedEndpoint(Uri uri)
    {
        ArgumentNullException.ThrowIfNull(uri);

        if (uri.Scheme != Uri.UriSchemeHttps)
        {
            return false;
        }

        var host = uri.Host;

        foreach (var allowed in AllowedExactHosts)
        {
            if (string.Equals(host, allowed, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        foreach (var suffix in AllowedHostSuffixes)
        {
            if (host.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
