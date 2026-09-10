using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace KarraMatcher.Infrastructure.Push;

/// <summary>
/// Intyget som säger till push-tjänsten att utskicket kommer från oss (VAPID, RFC 8292).
///
/// <h3>Vad intyget bevisar</h3>
///
/// <para>
/// Att den som skickar har den privata nyckel vars publika motsvarighet webbläsaren
/// godkände när den prenumererade. Utan det hade vem som helst som fick tag i en
/// push-adress kunnat skicka notiser till telefonen i appens namn.
/// </para>
///
/// <h3>Mottagaren är tjänsten, inte enheten</h3>
///
/// <para>
/// <c>aud</c> är push-tjänstens ursprung — <c>https://fcm.googleapis.com</c>, inte hela
/// adressen. Att skicka hela adressen hade läckt enhetens identitet in i ett fält som inte
/// behöver den, och tjänsterna avvisar det dessutom.
/// </para>
/// </summary>
internal static class VapidTokens
{
    /// <summary>
    /// Tolv timmar. Standarden tillåter högst ett dygn; kortare kostar ingenting eftersom
    /// intyget skapas per utskick.
    /// </summary>
    private static readonly TimeSpan Lifetime = TimeSpan.FromHours(12);

    private static readonly byte[] Header =
        Encoding.ASCII.GetBytes("""{"typ":"JWT","alg":"ES256"}""");

    /// <summary>Skapar rubriken <c>Authorization: vapid t=..., k=...</c>.</summary>
    public static string AuthorizationHeader(
        Uri endpoint,
        string subject,
        string publicKey,
        string privateKey,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(endpoint);

        var token = Create(endpoint, subject, privateKey, publicKey, now);

        return $"vapid t={token}, k={publicKey}";
    }

    private static string Create(
        Uri endpoint,
        string subject,
        string privateKey,
        string publicKey,
        DateTimeOffset now)
    {
        var payload = JsonSerializer.SerializeToUtf8Bytes(new Dictionary<string, object>
        {
            ["aud"] = endpoint.GetLeftPart(UriPartial.Authority),
            ["exp"] = now.Add(Lifetime).ToUnixTimeSeconds(),
            ["sub"] = subject,
        });

        var signingInput = $"{Base64Url.EncodeToString(Header)}.{Base64Url.EncodeToString(payload)}";

        using var key = Import(privateKey, publicKey);

        /*
         * IeeeP1363 och inte DER. ES256 i en JWT ar r och s efter varandra, 64 byte -- en
         * DER-kodad signatur ar giltig kryptografiskt och avvisas anda av varje mottagare.
         */
        var signature = key.SignData(
            Encoding.ASCII.GetBytes(signingInput),
            HashAlgorithmName.SHA256,
            DSASignatureFormat.IeeeP1363FixedFieldConcatenation);

        return $"{signingInput}.{Base64Url.EncodeToString(signature)}";
    }

    /// <summary>
    /// Läser in nyckelparet ur konfigurationens två base64url-strängar.
    ///
    /// <para>
    /// Båda behövs: .NET vill ha kurvpunkten för att kunna signera, och den finns bara i
    /// den publika nyckeln. Att kräva båda är dessutom en kontroll i sig — ett par som inte
    /// hör ihop faller här i stället för att avvisas av push-tjänsten.
    /// </para>
    /// </summary>
    private static ECDsa Import(string privateKey, string publicKey)
    {
        var point = Base64Url.DecodeFromChars(publicKey);

        if (point.Length != 65 || point[0] != 0x04)
        {
            throw new InvalidOperationException(
                "Push:PublicKey ar inte en 65 byte okomprimerad P-256-punkt.");
        }

        var key = ECDsa.Create();

        key.ImportParameters(new ECParameters
        {
            Curve = ECCurve.NamedCurves.nistP256,
            D = Base64Url.DecodeFromChars(privateKey),
            Q = new ECPoint { X = point[1..33], Y = point[33..65] },
        });

        return key;
    }
}
