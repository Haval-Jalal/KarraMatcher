using KarraMatcher.Application.Abstractions.Persistence;

using Microsoft.Extensions.Logging;

namespace KarraMatcher.Application.Features.Invitations;

/// <summary>
/// Gallrar döda inbjudningar (§KM.6, `#585`).
///
/// <para>
/// En inbjudan bär en e-postadress i klartext — adressen den skickades till. När en inbjudan
/// gått ut eller återkallats fyller den ingen funktion längre, men adressen ligger kvar.
/// Den som accepterat är ett medlemskap och rörs inte (den kaskaderar bort med sitt konto);
/// resten tas bort <see cref="RetentionPeriod"/> efter att de gått ut. Bara antal loggas —
/// aldrig adressen.
/// </para>
/// </summary>
public sealed partial class InvitationRetentionService(
    IInvitationRetentionRepository repository,
    TimeProvider clock,
    ILogger<InvitationRetentionService> logger)
{
    /// <summary>Hur länge en död inbjudan ligger kvar efter sin utgång (30 dagar, `#585`).</summary>
    public static readonly TimeSpan RetentionPeriod = TimeSpan.FromDays(30);

    public async Task<int> PurgeAsync(CancellationToken cancellationToken)
    {
        var cutoff = clock.GetUtcNow().UtcDateTime - RetentionPeriod;

        var purged = await repository.PurgeDeadOlderThanAsync(cutoff, cancellationToken)
            .ConfigureAwait(false);

        if (purged > 0)
        {
            LogPurged(purged);
        }

        return purged;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Gallrade {Count} doda inbjudningar.")]
    private partial void LogPurged(int count);
}
