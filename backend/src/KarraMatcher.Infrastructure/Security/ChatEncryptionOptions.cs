using System.ComponentModel.DataAnnotations;

namespace KarraMatcher.Infrastructure.Security;

/// <summary>
/// Nyckeln som krypterar chattens fritext i vila (§KM.10). Läses ur sektionen <c>Chat</c>
/// (<c>Chat:EncryptionKey</c> / <c>Chat__EncryptionKey</c>), aldrig hårdkodad — se backend/.env.example.
///
/// <para>
/// Nyckeln är 32 byte (AES-256) base64-kodad. Den valideras vid start (<c>ValidateOnStart</c>):
/// en saknad eller felaktig nyckel ska fälla driftsättningen medan någon tittar, inte tyst göra
/// varje meddelande oläsbart. Nyckeln får aldrig delas med signeringsnyckeln eller bytas utan att
/// befintliga rader migreras — ett byte gör gammal text oläsbar.
/// </para>
/// </summary>
public sealed class ChatEncryptionOptions
{
    public const string SectionName = "Chat";

    [Required(ErrorMessage = "Chat:EncryptionKey saknas — se backend/.env.example.")]
    public string EncryptionKey { get; set; } = string.Empty;
}
