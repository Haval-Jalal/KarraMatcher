using KarraMatcher.Domain.Attendance;

namespace KarraMatcher.Application.Abstractions.Persistence;

/// <summary>
/// Läser och skriver kallelsen och dess svar (`#57`).
///
/// <para>
/// Skild från <see cref="IAttendanceRepository"/>, som bara rör grindens flagga. Kallelsen
/// och svaren är den faktiska funktionen bakom grinden — de hör ihop som en enhet: ett svar
/// utan en kallelse hör ingenstans.
/// </para>
/// </summary>
public interface IAttendanceCallRepository
{
    /// <summary>
    /// Hör matchen till laget med den slugen? Falskt både när matchen inte finns och när
    /// den tillhör ett annat lag — samma svar, så en tränare inte kan kartlägga andra lags
    /// matcher genom att prova.
    /// </summary>
    public Task<bool> MatchBelongsToTeamAsync(
        Guid matchId,
        string slug,
        CancellationToken cancellationToken);

    /// <summary>Sant när kallelsen redan är öppnad för matchen.</summary>
    public Task<bool> CallExistsAsync(Guid matchId, CancellationToken cancellationToken);

    public Task AddCallAsync(AttendanceCall attendanceCall, CancellationToken cancellationToken);

    /// <summary>Matchens avspark i UTC, eller null när matchen inte finns.</summary>
    public Task<DateTime?> FindKickoffUtcAsync(Guid matchId, CancellationToken cancellationToken);

    /// <summary>
    /// Kontots svar på matchen, spårat så att det går att ändra. Null när inget finns än.
    /// </summary>
    public Task<AttendanceResponse?> FindResponseAsync(
        Guid matchId,
        Guid accountId,
        CancellationToken cancellationToken);

    public Task AddResponseAsync(AttendanceResponse response, CancellationToken cancellationToken);

    /// <summary>
    /// Alla svar på en match, äldst först — den som svarade först syns först. Endast läsning
    /// (tränarens summering, `#58`).
    /// </summary>
    public Task<IReadOnlyList<AttendanceResponse>> ListResponsesForMatchAsync(
        Guid matchId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Kontona som förväntas svara på matchen: de som prenumererar på lagets notiser och
    /// har ett konto (`#58`, §KM.1).
    ///
    /// <para>
    /// Det är den enda konto-baserade lag-kopplingen vi har (den infördes i <c>#63</c>) —
    /// och den som kan ta emot en påminnelse. "Inte svarat" räknas mot den här mängden: en
    /// förälder som följer laget men inte svarat. En gäst utan konto räknas inte, och kan
    /// heller inte nås av en notis.
    /// </para>
    /// </summary>
    public Task<IReadOnlyList<Guid>> ListExpectedResponderAccountIdsAsync(
        Guid matchId,
        CancellationToken cancellationToken);

    /// <summary>Matchens lag, eller null när matchen inte finns. För att rikta påminnelsen (`#65`).</summary>
    public Task<Guid?> FindTeamIdAsync(Guid matchId, CancellationToken cancellationToken);

    public Task SaveChangesAsync(CancellationToken cancellationToken);
}
