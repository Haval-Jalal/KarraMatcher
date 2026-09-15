namespace KarraMatcher.Application.Abstractions.Persistence;

/// <summary>
/// Gallringen av push-prenumerationer (`#68`, säkerhetschecklistan 9.8).
///
/// <para>
/// Egen abstraktion, av samma skäl som samåkningens gallring: det här är en tidsstyrd
/// radering som sker utan att någon bett om den, inte en del av prenumerationsflödet. Den
/// döda prenumerationen — den vars push-tjänst svarat 404/410 — städas redan reaktivt när
/// ett utskick misslyckas (<c>PushDispatchWorker</c>). Den här sveper i stället upp de som
/// aldrig får ett utskick och därför aldrig hör av sig: en telefon som bytts eller en
/// webbläsare vars lagring rensats lämnar en rad som annars skulle ligga kvar för evigt.
/// </para>
/// </summary>
public interface IPushRetentionRepository
{
    /// <summary>
    /// Raderar prenumerationer som varit tysta sedan före <paramref name="cutoffUtc"/>.
    ///
    /// <para>
    /// "Tyst" mäts på <c>LastUsedUtc</c>, och för den som aldrig fått ett utskick på
    /// <c>CreatedUtc</c> i stället (<c>LastUsedUtc</c> sätts först vid en lyckad leverans).
    /// Utan det fallet hade en prenumeration som aldrig använts aldrig gallrats.
    /// </para>
    /// </summary>
    /// <returns>Antal raderade prenumerationer.</returns>
    public Task<int> PurgeInactiveAsync(DateTime cutoffUtc, CancellationToken cancellationToken);
}
