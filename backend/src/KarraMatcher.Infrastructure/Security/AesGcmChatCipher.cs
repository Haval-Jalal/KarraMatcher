using System.Security.Cryptography;
using System.Text;

using KarraMatcher.Application.Abstractions.Security;

using Microsoft.Extensions.Options;

namespace KarraMatcher.Infrastructure.Security;

/// <summary>
/// AES-256-GCM-kryptering av chattens fritext i vila (<see cref="IChatTextCipher"/>, §KM.10).
///
/// <para>
/// GCM ger både sekretess och äkthet (en manipulerad rad avvisas). Varje värde får en egen
/// slumpad 96-bitars nonce, så samma text två gånger ger olika chiffer. Formatet är
/// <c>kmenc1:</c> + base64(<c>nonce | tag | chiffer</c>) — markören gör att avkrypteringen känner
/// igen sitt eget format och lämnar äldre klartext orörd.
/// </para>
/// </summary>
public sealed class AesGcmChatCipher : IChatTextCipher
{
    /// <summary>Versions-märke, så formatet kan utvecklas utan att gammal text blir oläsbar.</summary>
    private const string Marker = "kmenc1:";

    private const int KeyBytes = 32; // AES-256
    private const int NonceBytes = 12; // 96-bitars nonce — GCM:s standard
    private const int TagBytes = 16;

    private readonly byte[] _key;

    public AesGcmChatCipher(IOptions<ChatEncryptionOptions> options)
        : this(DecodeKey(options))
    {
    }

    /// <summary>För design-time och tester: nyckeln som råa byte.</summary>
    internal AesGcmChatCipher(byte[] key)
    {
        ArgumentNullException.ThrowIfNull(key);

        if (key.Length != KeyBytes)
        {
            throw new ArgumentException(
                $"Chat-nyckeln måste vara {KeyBytes} byte (AES-256), var {key.Length}.", nameof(key));
        }

        _key = key;
    }

    public string Encrypt(string plaintext)
    {
        ArgumentNullException.ThrowIfNull(plaintext);

        // Tomt lagras som tomt — inget att dölja, och tomma strängar ska förbli tomma.
        if (plaintext.Length == 0)
        {
            return plaintext;
        }

        var nonce = RandomNumberGenerator.GetBytes(NonceBytes);
        var plain = Encoding.UTF8.GetBytes(plaintext);
        var cipher = new byte[plain.Length];
        var tag = new byte[TagBytes];

        using (var aes = new AesGcm(_key, TagBytes))
        {
            aes.Encrypt(nonce, plain, cipher, tag);
        }

        var blob = new byte[NonceBytes + TagBytes + cipher.Length];
        Buffer.BlockCopy(nonce, 0, blob, 0, NonceBytes);
        Buffer.BlockCopy(tag, 0, blob, NonceBytes, TagBytes);
        Buffer.BlockCopy(cipher, 0, blob, NonceBytes + TagBytes, cipher.Length);

        return Marker + Convert.ToBase64String(blob);
    }

    public string Decrypt(string stored)
    {
        ArgumentNullException.ThrowIfNull(stored);

        // Tålig mot äldre klartext (skriven innan krypteringen fanns) och mot tomt.
        if (!stored.StartsWith(Marker, StringComparison.Ordinal))
        {
            return stored;
        }

        var blob = Convert.FromBase64String(stored[Marker.Length..]);
        var nonce = blob.AsSpan(0, NonceBytes);
        var tag = blob.AsSpan(NonceBytes, TagBytes);
        var cipher = blob.AsSpan(NonceBytes + TagBytes);
        var plain = new byte[cipher.Length];

        using (var aes = new AesGcm(_key, TagBytes))
        {
            aes.Decrypt(nonce, cipher, tag, plain);
        }

        return Encoding.UTF8.GetString(plain);
    }

    private static byte[] DecodeKey(IOptions<ChatEncryptionOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);

        try
        {
            return Convert.FromBase64String(options.Value.EncryptionKey);
        }
        catch (FormatException ex)
        {
            throw new InvalidOperationException(
                "Chat:EncryptionKey är inte giltig base64 — se backend/.env.example.", ex);
        }
    }
}
