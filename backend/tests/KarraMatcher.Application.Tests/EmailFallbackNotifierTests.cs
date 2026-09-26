using KarraMatcher.Application.Abstractions.Email;
using KarraMatcher.Application.Abstractions.Persistence;
using KarraMatcher.Application.Features.Auth;
using KarraMatcher.Application.Features.Push;

using Microsoft.Extensions.Options;

namespace KarraMatcher.Application.Tests;

/// <summary>
/// E-postfallbacken för kritiska notiser.
///
/// <para>
/// Mejl går bara vid <b>kritiska</b> kategorier (händelse ändrad/inställd, ny kallelse) och
/// bara till dem repositoryt lämnar tillbaka — de som saknar push. Mejlet bär notisens neutrala
/// rubrik och rad plus en länk in i appen, aldrig något om ett barn (§KM.1/§KM.10).
/// </para>
/// </summary>
public sealed class EmailFallbackNotifierTests
{
    private static readonly EmailRecipient[] TwoRecipients =
    [
        new(Guid.NewGuid(), "anna@example.com"),
        new(Guid.NewGuid(), "bo@example.com"),
    ];

    private static EmailFallbackNotifier Notifier(IEmailFallbackRepository repository, IEmailSender email) =>
        new(repository, email, Options.Create(new AuthOptions { AppBaseUrl = "https://app.example" }));

    [Fact]
    public async Task KritiskLagnotis_MejlarVarMottagareUtanPush()
    {
        var repository = new FakeRepository(TwoRecipients);
        var email = new RecordingEmailSender();

        var dispatch = PushDispatch.ToTeam(
            Guid.NewGuid(),
            PushCategory.EventChange,
            new PushMessage("Matchtiden har ändrats", "Lördag 12:00", "/handelse/abc"));

        await Notifier(repository, email).SendAsync(dispatch, CancellationToken.None);

        Assert.True(repository.TeamAsked);
        Assert.Equal(2, email.Sent.Count);

        var first = email.Sent[0];
        Assert.Equal("anna@example.com", first.To);
        Assert.Equal("Matchtiden har ändrats", first.Subject);
        Assert.Contains("Lördag 12:00", first.Body, StringComparison.Ordinal);
        Assert.Contains("https://app.example/handelse/abc", first.Body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task IckeKritisk_Samakning_MejlarInte()
    {
        // Samåkning är inte ett besked man inte får missa — ingen fallback, repositoryt frågas inte.
        var repository = new FakeRepository(TwoRecipients);
        var email = new RecordingEmailSender();

        var dispatch = PushDispatch.ToTeam(
            Guid.NewGuid(),
            PushCategory.Carpool,
            new PushMessage("Ny samåkning", "Till lördagens match", "/handelse/abc"));

        await Notifier(repository, email).SendAsync(dispatch, CancellationToken.None);

        Assert.False(repository.TeamAsked);
        Assert.Empty(email.Sent);
    }

    [Fact]
    public async Task IngaMottagareUtanPush_MejlarInte()
    {
        // Alla nås av push → ingen att mejla.
        var repository = new FakeRepository([]);
        var email = new RecordingEmailSender();

        var dispatch = PushDispatch.ToTeam(
            Guid.NewGuid(),
            PushCategory.Kallelse,
            new PushMessage("Ny kallelse", "Svara i appen", "/handelse/abc"));

        await Notifier(repository, email).SendAsync(dispatch, CancellationToken.None);

        Assert.Empty(email.Sent);
    }

    [Fact]
    public async Task KontoriktadKallelse_AnvanderKontolistan()
    {
        var repository = new FakeRepository(TwoRecipients);
        var email = new RecordingEmailSender();

        var dispatch = PushDispatch.ToAccounts(
            Guid.NewGuid(),
            [Guid.NewGuid()],
            PushCategory.Kallelse,
            new PushMessage("Ny kallelse", "", "/handelse/abc"));

        await Notifier(repository, email).SendAsync(dispatch, CancellationToken.None);

        Assert.True(repository.AccountsAsked);
        Assert.False(repository.TeamAsked);
        Assert.Equal(2, email.Sent.Count);
        // Tom body → bara länken.
        Assert.Equal("https://app.example/handelse/abc", email.Sent[0].Body);
    }

    private sealed class FakeRepository(IReadOnlyList<EmailRecipient> recipients) : IEmailFallbackRepository
    {
        public bool TeamAsked { get; private set; }

        public bool AccountsAsked { get; private set; }

        public Task<IReadOnlyList<EmailRecipient>> ListForTeamAsync(
            Guid teamId, PushCategory category, CancellationToken cancellationToken)
        {
            TeamAsked = true;
            return Task.FromResult(recipients);
        }

        public Task<IReadOnlyList<EmailRecipient>> ListForAccountsAsync(
            Guid teamId,
            IReadOnlyCollection<Guid> accountIds,
            PushCategory category,
            CancellationToken cancellationToken)
        {
            AccountsAsked = true;
            return Task.FromResult(recipients);
        }
    }

    private sealed class RecordingEmailSender : IEmailSender
    {
        public List<(string To, string Subject, string Body)> Sent { get; } = [];

        public Task SendAsync(string recipient, string subject, string body, CancellationToken cancellationToken)
        {
            Sent.Add((recipient, subject, body));
            return Task.CompletedTask;
        }
    }
}
