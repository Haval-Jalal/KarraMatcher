using System.Buffers.Binary;
using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;

using KarraMatcher.Infrastructure.Push;
using KarraMatcher.Infrastructure.Security;

namespace KarraMatcher.Infrastructure.Tests;

/// <summary>
/// Krypteringen av en notis (`#61`, RFC 8291).
///
/// <para>
/// Ett fel här ger inget felmeddelande någonstans: push-tjänsten tar emot klumpen, skickar
/// den vidare, och telefonen slänger den tyst. Det är det svåraste sättet att ha fel på —
/// och därför det som måste prövas i ett test i stället för på en lördagsmorgon.
/// </para>
///
/// <para>
/// Testet dekrypterar med mottagarens nyckel, alltså åt andra hållet, i stället för att
/// jämföra mot en förväntad byte-sträng. Två skäl: en hårdkodad sträng säger ingenting om
/// <em>varför</em> den ser ut så, och en dekryptering som utgår från standarden fångar även
/// de fel där kodningen är konsekvent men fel.
/// </para>
/// </summary>
public sealed class WebPushEncryptionTests
{
    /// <summary>Vad servicen faktiskt skickar: en kort rubrik och en rad till.</summary>
    private const string Payload =
        """{"title":"Matchen ar installd","body":"Lordag 14:00 mot Torslanda","url":"/match/1"}""";

    /// <summary>En webbläsare som prenumererar: ett nyckelpar och en auth-hemlighet.</summary>
    private static (ECDiffieHellman Key, byte[] PublicPoint, byte[] Auth) Browser()
    {
        var key = ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256);
        var parameters = key.ExportParameters(false);

        var point = new byte[65];
        point[0] = 0x04;
        parameters.Q.X!.CopyTo(point, 1);
        parameters.Q.Y!.CopyTo(point, 33);

        return (key, point, RandomNumberGenerator.GetBytes(16));
    }

    [Fact]
    public void Nyttolasten_GarAttOppnaMedMottagarensNyckel()
    {
        /*
         * Hela poangen med Web Push: Google och Apple ser en ogenomtranglig klump, men
         * webblasaren som prenumererade kan lasa den. Gar det inte att oppna har kommer
         * notisen aldrig fram, och ingenting sager till.
         */
        var (browserKey, browserPublic, auth) = Browser();

        using (browserKey)
        {
            var body = Encrypt(browserPublic, auth, Encoding.UTF8.GetBytes(Payload));

            var opened = Decrypt(body, browserKey, browserPublic, auth);

            Assert.Equal(Payload, Encoding.UTF8.GetString(opened));
        }
    }

    [Fact]
    public void Kroppen_HarDenFormStandardenKraver()
    {
        /*
         * Salt, poststorlek, nyckellangd och nyckel -- i den ordningen och med de
         * langderna. Mottagaren laser hardkodade positioner, sa en byte fel gor hela
         * nyttolasten olaslig utan att nagot ser trasigt ut har.
         */
        var (browserKey, browserPublic, auth) = Browser();

        using (browserKey)
        {
            var body = Encrypt(browserPublic, auth, Encoding.UTF8.GetBytes(Payload));

            Assert.Equal(4096u, BinaryPrimitives.ReadUInt32BigEndian(body.AsSpan(16, 4)));
            Assert.Equal(65, body[20]);
            Assert.Equal(0x04, body[21]);
        }
    }

    [Fact]
    public void Tva_Utskick_AvSammaText_SerOlikaUt()
    {
        // Nytt salt och ny efemar nyckel varje gang. Identiska kryptogram hade lackt att
        // samma notis gick till flera enheter, och gjort avlyssning enklare an den ska vara.
        var (browserKey, browserPublic, auth) = Browser();

        using (browserKey)
        {
            var first = Encrypt(browserPublic, auth, Encoding.UTF8.GetBytes(Payload));
            var second = Encrypt(browserPublic, auth, Encoding.UTF8.GetBytes(Payload));

            Assert.NotEqual(first, second);
        }
    }

    [Fact]
    public void FelAuthHemlighet_GerIngenLasbarText()
    {
        // Auth-hemligheten ar en del av nyckelharledningen. Ar den fel ska det bli obegripligt,
        // inte "nastan ratt" -- annars vore krypteringen en fasad.
        var (browserKey, browserPublic, auth) = Browser();

        using (browserKey)
        {
            var body = Encrypt(browserPublic, auth, Encoding.UTF8.GetBytes(Payload));

            Assert.ThrowsAny<CryptographicException>(
                () => Decrypt(body, browserKey, browserPublic, RandomNumberGenerator.GetBytes(16)));
        }
    }

    [Fact]
    public void VapidNyckelparet_GarAttAnvandaTillSignering()
    {
        /*
         * Nycklarna fran `--vapid` maste ga att lasa in igen och signera med. Ett par som
         * genereras men inte gar att anvanda hade upptackts forst nar forsta notisen skulle
         * skickas -- alltsa i drift.
         */
        var (publicKey, privateKey) = VapidKeys.Generate();

        var point = Base64Url.DecodeFromChars(publicKey);

        using var key = ECDsa.Create();

        key.ImportParameters(new ECParameters
        {
            Curve = ECCurve.NamedCurves.nistP256,
            D = Base64Url.DecodeFromChars(privateKey),
            Q = new ECPoint { X = point[1..33], Y = point[33..65] },
        });

        var data = Encoding.ASCII.GetBytes("karra");

        var signature = key.SignData(
            data,
            HashAlgorithmName.SHA256,
            DSASignatureFormat.IeeeP1363FixedFieldConcatenation);

        Assert.Equal(64, signature.Length);
        Assert.True(key.VerifyData(
            data,
            signature,
            HashAlgorithmName.SHA256,
            DSASignatureFormat.IeeeP1363FixedFieldConcatenation));
    }

    /// <summary>
    /// Krypteringen är intern i Infrastructure; testprojektet ser den via
    /// <c>InternalsVisibleTo</c>, som redan finns i csproj-filen.
    /// </summary>
    private static byte[] Encrypt(byte[] browserPublic, byte[] auth, byte[] plaintext) =>
        WebPushEncryption.Encrypt(browserPublic, auth, plaintext);

    /// <summary>
    /// Öppnar en krypterad kropp, skriven efter standarden och inte efter vår implementation.
    ///
    /// <para>
    /// Det är avsiktligt att den här koden är skriven separat: delade den härledning med det
    /// som testas skulle ett fel i härledningen se rätt ut åt båda hållen.
    /// </para>
    /// </summary>
    private static byte[] Decrypt(
        byte[] body,
        ECDiffieHellman browserKey,
        byte[] browserPublic,
        byte[] auth)
    {
        var salt = body[..16];
        var keyLength = body[20];
        var serverPublic = body[21..(21 + keyLength)];
        var ciphertext = body[(21 + keyLength)..^16];
        var tag = body[^16..];

        using var server = ECDiffieHellman.Create();

        server.ImportParameters(new ECParameters
        {
            Curve = ECCurve.NamedCurves.nistP256,
            Q = new ECPoint { X = serverPublic[1..33], Y = serverPublic[33..65] },
        });

        var shared = browserKey.DeriveRawSecretAgreement(server.PublicKey);

        var authPrk = HKDF.Extract(HashAlgorithmName.SHA256, shared, auth);

        var keyInfo = Encoding.ASCII.GetBytes("WebPush: info\0")
            .Concat(browserPublic)
            .Concat(serverPublic)
            .ToArray();

        var ikm = HKDF.Expand(HashAlgorithmName.SHA256, authPrk, 32, keyInfo);
        var prk = HKDF.Extract(HashAlgorithmName.SHA256, ikm, salt);

        var contentKey = HKDF.Expand(
            HashAlgorithmName.SHA256, prk, 16, Encoding.ASCII.GetBytes("Content-Encoding: aes128gcm\0"));

        var nonce = HKDF.Expand(
            HashAlgorithmName.SHA256, prk, 12, Encoding.ASCII.GetBytes("Content-Encoding: nonce\0"));

        var record = new byte[ciphertext.Length];

        using var aes = new AesGcm(contentKey, tag.Length);
        aes.Decrypt(nonce, ciphertext, tag, record);

        // Sista byten ar avgransaren 0x02, inte innehall.
        return record[..^1];
    }
}
