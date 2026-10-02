using KarraMatcher.Application.Features.Push;

namespace KarraMatcher.Application.Tests;

/// <summary>
/// Host-allowlistan för push-adresser (`#581`, SSRF). Bara de etablerade pushtjänsterna släpps
/// igenom; interna värdar, IP-literaler, http och lookalike-domäner nekas.
/// </summary>
public sealed class PushEndpointPolicyTests
{
    [Theory]
    [InlineData("https://fcm.googleapis.com/fcm/send/abc123")]
    [InlineData("https://updates.push.services.mozilla.com/wpush/v2/abc")]
    [InlineData("https://web.push.apple.com/Qabc123")]
    [InlineData("https://ABC.notify.windows.com/w/?token=xyz")]
    public void KandaPushtjanster_Slapps(string endpoint)
    {
        Assert.True(PushEndpointPolicy.IsAllowedEndpoint(endpoint));
    }

    [Theory]
    [InlineData("https://evil.example.com/steal")] // godtycklig host
    [InlineData("https://169.254.169.254/latest/meta-data")] // metadata-IP
    [InlineData("https://10.0.0.5/internal")] // privat IP
    [InlineData("https://localhost/x")] // loopback
    [InlineData("http://fcm.googleapis.com/fcm/send/abc")] // inte https
    [InlineData("https://fcm.googleapis.com.evil.com/abc")] // lookalike-suffix
    [InlineData("https://notfcm.googleapis.com/abc")] // lookalike-prefix
    [InlineData("https://fcm.googleapis.com@evil.com/abc")] // userinfo-trick (host = evil.com)
    [InlineData("/relativ/adress")] // inte absolut
    [InlineData("")]
    public void OkandaEllerFarligaAdresser_Nekas(string endpoint)
    {
        Assert.False(PushEndpointPolicy.IsAllowedEndpoint(endpoint));
    }
}
