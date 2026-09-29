using KarraMatcher.Api.Diagnostics;

namespace KarraMatcher.Api.Integration.Tests;

/// <summary>
/// Kalender-nyckeln får aldrig loggas (§KM.10, `#401`). Redigeringen byter ut nyckel-segmentet
/// i feed-sökvägen men lämnar allt annat orört.
/// </summary>
public sealed class RequestLogRedactionTests
{
    [Fact]
    public void Redact_KalenderFeed_DoljerNyckeln()
    {
        const string token = "abc123def456ghi789";
        var redacted = RequestLogRedaction.Redact($"/api/v1/kalender/{token}.ics");

        Assert.Equal("/api/v1/kalender/[redacted].ics", redacted);
        Assert.DoesNotContain(token, redacted, StringComparison.Ordinal);
    }

    [Theory]
    // De autentiserade kalender-vägarna bär ingen nyckel i sökvägen och rörs inte.
    [InlineData("/api/v1/kalender/min")]
    [InlineData("/api/v1/kalender/aterkalla")]
    // Andra endpoints lämnas oförändrade.
    [InlineData("/api/v1/hem")]
    [InlineData("/api/v1/teams/gul/chat/messages")]
    [InlineData("/health")]
    public void Redact_AnnanVag_LamnasOrord(string path)
    {
        Assert.Equal(path, RequestLogRedaction.Redact(path));
    }
}
