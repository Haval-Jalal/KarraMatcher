using System.Security.Cryptography;

using KarraMatcher.Infrastructure.Security;

namespace KarraMatcher.Infrastructure.Tests;

/// <summary>
/// AES-GCM-krypteringen av chattens fritext (§KM.10): rundtur, slumpad nonce, tålighet mot äldre
/// klartext, och att fel nyckel inte kan läsa.
/// </summary>
public sealed class AesGcmChatCipherTests
{
    private static AesGcmChatCipher Cipher() => new(new byte[32]);

    [Fact]
    public void Roundtrip_KrypterarOchAvkrypterarTillbaka()
    {
        var cipher = Cipher();
        const string plaintext = "Vem kör till Öckerö? 🚗";

        var encrypted = cipher.Encrypt(plaintext);

        Assert.StartsWith("kmenc1:", encrypted);
        Assert.DoesNotContain("Öckerö", encrypted, StringComparison.Ordinal);
        Assert.Equal(plaintext, cipher.Decrypt(encrypted));
    }

    [Fact]
    public void SammaText_TvaGanger_GerOlikaChiffer()
    {
        var cipher = Cipher();

        // Slumpad nonce per värde → samma text ger olika chiffer (ingen läckande mönster).
        Assert.NotEqual(cipher.Encrypt("hej"), cipher.Encrypt("hej"));
    }

    [Fact]
    public void Decrypt_AldreKlartextUtanMarkor_LamnasOforandrad()
    {
        var cipher = Cipher();

        Assert.Equal("gammal rad utan markör", cipher.Decrypt("gammal rad utan markör"));
    }

    [Fact]
    public void TomStrang_ForblirTom()
    {
        var cipher = Cipher();

        Assert.Equal(string.Empty, cipher.Encrypt(string.Empty));
        Assert.Equal(string.Empty, cipher.Decrypt(string.Empty));
    }

    [Fact]
    public void FelNyckel_KanInteLasa()
    {
        var encrypted = new AesGcmChatCipher(new byte[32]).Encrypt("hemligt");

        var otherKey = new byte[32];
        otherKey[0] = 1;

        Assert.ThrowsAny<CryptographicException>(
            () => new AesGcmChatCipher(otherKey).Decrypt(encrypted));
    }
}
