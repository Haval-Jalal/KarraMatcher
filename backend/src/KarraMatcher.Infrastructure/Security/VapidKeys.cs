using System.Buffers.Text;
using System.Security.Cryptography;

namespace KarraMatcher.Infrastructure.Security;

/// <summary>
/// Genererar ett VAPID-nyckelpar (`#60`, säkerhetschecklistan 7.5).
///
/// <h3>Varför nycklarna skapas här och inte hämtas från ett bibliotek</h3>
///
/// <para>
/// Det här är fyra rader kryptografi som .NET redan kan: en P-256-nyckel, exporterad i det
/// format webbläsarnas push-tjänster förväntar sig. Ett paket för uppgiften hade tillfört
/// en licensyta och ett beroende att hålla uppdaterat för något som inte kommer att ändras
/// — VAPID-formatet är låst av standarden.
/// </para>
///
/// <h3>Formatet är inte godtyckligt</h3>
///
/// <para>
/// Den publika nyckeln är punkten okomprimerad: <c>0x04</c> följt av X och Y, 65 byte,
/// base64url utan utfyllnad. Det är exakt vad <c>applicationServerKey</c> i webbläsaren
/// vill ha. Den privata är skalären <c>D</c>, 32 byte, samma kodning.
/// </para>
/// </summary>
public static class VapidKeys
{
    /// <summary>Skapar ett nytt par. Kör en gång per miljö, spara i secret store.</summary>
    public static (string PublicKey, string PrivateKey) Generate()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);

        var parameters = key.ExportParameters(includePrivateParameters: true);

        var x = parameters.Q.X ?? throw new InvalidOperationException("Nyckeln saknar X.");
        var y = parameters.Q.Y ?? throw new InvalidOperationException("Nyckeln saknar Y.");
        var d = parameters.D ?? throw new InvalidOperationException("Nyckeln saknar D.");

        var point = new byte[1 + x.Length + y.Length];
        point[0] = 0x04;
        x.CopyTo(point, 1);
        y.CopyTo(point, 1 + x.Length);

        return (Base64Url.EncodeToString(point), Base64Url.EncodeToString(d));
    }
}
