using System.Buffers.Binary;
using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;

namespace KarraMatcher.Infrastructure.Push;

/// <summary>
/// Krypterar en notis åt en enskild webbläsare, enligt RFC 8291 (`aes128gcm`).
///
/// <h3>Varför innehållet krypteras</h3>
///
/// <para>
/// Notisen passerar Google, Apple eller Mozilla på vägen till telefonen. Krypteringen gör
/// att de bara ser en ogenomtränglig klump — nyckeln finns bara hos webbläsaren som
/// prenumererade. Det är därför en push-tjänst kan användas utan att man litar på den.
/// </para>
///
/// <h3>Varför den är handskriven</h3>
///
/// <para>
/// Allt som behövs finns i .NET: ECDH, HKDF och AES-GCM. Ett paket för uppgiften hade
/// tillfört en licensyta och ett beroende att hålla uppdaterat — och formatet är låst av
/// standarden, så det kommer inte att ändras. Samma avvägning som för dispatchern i
/// §KM.0 A9.
/// </para>
///
/// <h3>Vad som är utbytbart för testernas skull</h3>
///
/// <para>
/// Saltet och den efemära nyckeln är slumpade i drift. De går att skicka in, så att ett
/// test kan köra samma sak två gånger och få samma resultat — det är den enda vägen att
/// pröva kryptot utan att fråga en riktig push-tjänst.
/// </para>
/// </summary>
internal static class WebPushEncryption
{
    /// <summary>Postens storlek. 4096 räcker med marginal för en notis på två rader.</summary>
    private const int RecordSize = 4096;

    private static readonly byte[] KeyInfoPrefix = Encoding.ASCII.GetBytes("WebPush: info\0");
    private static readonly byte[] CekInfo = Encoding.ASCII.GetBytes("Content-Encoding: aes128gcm\0");
    private static readonly byte[] NonceInfo = Encoding.ASCII.GetBytes("Content-Encoding: nonce\0");

    /// <summary>
    /// Krypterar <paramref name="plaintext"/> åt mottagaren.
    /// </summary>
    /// <param name="userPublicKey">Webbläsarens <c>p256dh</c>, 65 byte okomprimerad punkt.</param>
    /// <param name="userAuth">Webbläsarens <c>auth</c>, 16 byte.</param>
    /// <param name="plaintext">Nyttolasten, redan serialiserad.</param>
    /// <param name="salt">16 byte. Slumpas när det utelämnas.</param>
    /// <param name="serverKey">Efemär nyckel. Skapas ny när den utelämnas — och ska så vara i drift.</param>
    public static byte[] Encrypt(
        byte[] userPublicKey,
        byte[] userAuth,
        byte[] plaintext,
        byte[]? salt = null,
        ECDiffieHellman? serverKey = null)
    {
        ArgumentNullException.ThrowIfNull(userPublicKey);
        ArgumentNullException.ThrowIfNull(userAuth);
        ArgumentNullException.ThrowIfNull(plaintext);

        salt ??= RandomNumberGenerator.GetBytes(16);

        var ownsKey = serverKey is null;
        var server = serverKey ?? ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256);

        try
        {
            var serverPublic = UncompressedPoint(server.ExportParameters(false));

            using var userKey = ImportPublicKey(userPublicKey);

            /*
             * Delad hemlighet ur ECDH, rakt av. DeriveRawSecretAgreement och inte
             * DeriveKeyMaterial: standarden vill ha den obehandlade punkten, medan
             * DeriveKeyMaterial hashar den at oss och ger fel varde.
             */
            var sharedSecret = server.DeriveRawSecretAgreement(userKey.PublicKey);

            // Steg ett: webblasarens auth-hemlighet ar salt, ECDH-hemligheten ar nyckelmaterial.
            var authPrk = HKDF.Extract(HashAlgorithmName.SHA256, sharedSecret, userAuth);

            var keyInfo = Concat(KeyInfoPrefix, userPublicKey, serverPublic);
            var inputKeyingMaterial = HKDF.Expand(HashAlgorithmName.SHA256, authPrk, 32, keyInfo);

            // Steg tva: det riktiga saltet, och darur nyckel och nonce.
            var prk = HKDF.Extract(HashAlgorithmName.SHA256, inputKeyingMaterial, salt);

            var contentKey = HKDF.Expand(HashAlgorithmName.SHA256, prk, 16, CekInfo);
            var nonce = HKDF.Expand(HashAlgorithmName.SHA256, prk, 12, NonceInfo);

            /*
             * 0x02 avslutar den sista posten. Utan avgransaren tolkar mottagaren nyttolasten
             * som att det finns en post till, och notisen tystnar utan felmeddelande.
             */
            var record = Concat(plaintext, [0x02]);

            var ciphertext = new byte[record.Length];
            var tag = new byte[16];

            using (var aes = new AesGcm(contentKey, tag.Length))
            {
                aes.Encrypt(nonce, record, ciphertext, tag);
            }

            return BuildBody(salt, serverPublic, ciphertext, tag);
        }
        finally
        {
            if (ownsKey)
            {
                server.Dispose();
            }
        }
    }

    /// <summary>
    /// Sätter ihop kroppen: salt, poststorlek, serverns publika nyckel och sedan innehållet.
    ///
    /// <para>
    /// Ordningen och längderna är fasta i standarden. Ett fel här ger inget felmeddelande
    /// någonstans — notisen kommer bara aldrig fram, vilket är det svåraste sättet att ha
    /// fel på.
    /// </para>
    /// </summary>
    private static byte[] BuildBody(byte[] salt, byte[] serverPublic, byte[] ciphertext, byte[] tag)
    {
        var body = new byte[16 + 4 + 1 + serverPublic.Length + ciphertext.Length + tag.Length];
        var span = body.AsSpan();

        salt.CopyTo(span);
        BinaryPrimitives.WriteUInt32BigEndian(span[16..20], RecordSize);
        span[20] = (byte)serverPublic.Length;
        serverPublic.CopyTo(span[21..]);
        ciphertext.CopyTo(span[(21 + serverPublic.Length)..]);
        tag.CopyTo(span[(21 + serverPublic.Length + ciphertext.Length)..]);

        return body;
    }

    /// <summary>Läser in webbläsarens publika nyckel ur den okomprimerade punkten.</summary>
    private static ECDiffieHellman ImportPublicKey(byte[] uncompressedPoint)
    {
        if (uncompressedPoint.Length != 65 || uncompressedPoint[0] != 0x04)
        {
            throw new ArgumentException(
                "Publik nyckel maste vara 65 byte okomprimerad punkt.",
                nameof(uncompressedPoint));
        }

        var key = ECDiffieHellman.Create();

        key.ImportParameters(new ECParameters
        {
            Curve = ECCurve.NamedCurves.nistP256,
            Q = new ECPoint
            {
                X = uncompressedPoint[1..33],
                Y = uncompressedPoint[33..65],
            },
        });

        return key;
    }

    private static byte[] UncompressedPoint(ECParameters parameters)
    {
        var x = parameters.Q.X ?? throw new InvalidOperationException("Nyckeln saknar X.");
        var y = parameters.Q.Y ?? throw new InvalidOperationException("Nyckeln saknar Y.");

        var point = new byte[65];
        point[0] = 0x04;
        x.CopyTo(point, 1);
        y.CopyTo(point, 33);

        return point;
    }

    private static byte[] Concat(params byte[][] parts)
    {
        var result = new byte[parts.Sum(part => part.Length)];
        var offset = 0;

        foreach (var part in parts)
        {
            part.CopyTo(result, offset);
            offset += part.Length;
        }

        return result;
    }

    /// <summary>Base64url utan utfyllnad — det enda kodningsformat Web Push använder.</summary>
    internal static byte[] Decode(string value) => Base64Url.DecodeFromChars(value);
}
