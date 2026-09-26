using Microsoft.Extensions.Logging;

namespace KarraMatcher.Api.Diagnostics;

/// <summary>
/// Skriver säkerhetsavvikelserna till Serilog som strukturerade varningar (§KM.10: aldrig
/// e-post, barnnamn eller fritext — bara metod och sökväg). En spik av dessa rader i
/// loggströmmen är det ett loggbaserat larm ska reagera på; se <c>DRIFTOVERVAKNING.md</c>.
/// </summary>
internal sealed partial class SecurityEventLog(ILogger<SecurityEventLog> logger) : ISecurityEventSink
{
    public void RateLimitRejected(string method, string path) =>
        LogRateLimitRejected(logger, method, path);

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Rate limit avvisade {Method} {Path}. Upprepade avslag är en attacksignal (§KM.0 A1).")]
    private static partial void LogRateLimitRejected(ILogger logger, string method, string path);
}
