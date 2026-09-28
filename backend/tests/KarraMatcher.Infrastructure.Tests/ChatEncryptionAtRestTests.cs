using KarraMatcher.Domain.Chat;
using KarraMatcher.Infrastructure.Persistence;
using KarraMatcher.Infrastructure.Security;

using Microsoft.EntityFrameworkCore;

namespace KarraMatcher.Infrastructure.Tests;

/// <summary>
/// Chattens fritext krypteras i vila (§KM.10). Att chiffret ser ut som chiffer bevisas i
/// <see cref="AesGcmChatCipherTests"/>; här låser vi att krypteringen faktiskt är <em>kopplad</em>
/// till kolumnerna Body och Reason (en värdekonverterare), och att en rundtur inte förvränger
/// texten. Tillsammans betyder det att det som når Postgres-kolumnen är krypterat.
/// </summary>
public sealed class ChatEncryptionAtRestTests
{
    private static Microsoft.EntityFrameworkCore.Metadata.IModel Model()
    {
        var options = new DbContextOptionsBuilder<KarraMatcherDbContext>()
            .UseNpgsql("Host=modell;Database=modell;Username=x;Password=y")
            .Options;

        using var context = new KarraMatcherDbContext(options, new AesGcmChatCipher(new byte[32]));
        return context.Model;
    }

    private static bool HasConverter<TEntity>(string property)
    {
        var entity = Model().FindEntityType(typeof(TEntity));
        Assert.NotNull(entity);
        return entity!.FindProperty(property)?.GetValueConverter() is not null;
    }

    [Fact]
    public void MeddelandetextOchAnmalan_HarKrypteringskonverterare()
    {
        Assert.True(HasConverter<ChatMessage>(nameof(ChatMessage.Body)));
        Assert.True(HasConverter<ChatReport>(nameof(ChatReport.Reason)));
    }

    [Fact]
    public async Task Rundtur_LasesTillbakaOforvanskad()
    {
        var options = new DbContextOptionsBuilder<KarraMatcherDbContext>()
            .UseInMemoryDatabase($"enc-{Guid.NewGuid()}")
            .Options;
        var key = new byte[32];
        key[0] = 7;
        const string plaintext = "Vem kör till Öckerö? 🚗";

        await using (var write = new KarraMatcherDbContext(options, new AesGcmChatCipher(key)))
        {
            write.ChatMessages.Add(new ChatMessage
            {
                Id = Guid.NewGuid(),
                AgeGroupId = Guid.NewGuid(),
                AuthorAccountId = Guid.NewGuid(),
                Body = plaintext,
                CreatedUtc = DateTime.UtcNow,
                PublishAtUtc = DateTime.UtcNow,
                PublishedUtc = DateTime.UtcNow,
            });
            await write.SaveChangesAsync(CancellationToken.None);
        }

        await using var read = new KarraMatcherDbContext(options, new AesGcmChatCipher(key));
        var message = await read.ChatMessages.SingleAsync(CancellationToken.None);

        Assert.Equal(plaintext, message.Body);
    }
}
