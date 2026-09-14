namespace KarraMatcher.Application.Features.Push;

/// <summary>
/// Det som faktiskt hamnar på en låsskärm (`#61`).
///
/// <h3>Nyttolasten är krypterad — och innehåller ändå ingenting känsligt</h3>
///
/// <para>
/// Web Push krypterar innehållet så att Google, Apple och Mozilla inte kan läsa det. Det
/// är ändå inte en anledning att lägga något känsligt där: notisen visas på en låsskärm
/// som vem som helst i rummet kan se, och den ligger kvar i notiscentret efteråt.
/// </para>
///
/// <para>
/// Därför gäller <b>samma tak som för allt annat</b>: aldrig något om ett barn (§KM.1),
/// aldrig spelarkortets innehåll (§KM.2), och aldrig en förälders fritext (§KM.12). "Ny
/// samåkning till lördagens match" är rätt nivå — den som vill veta mer öppnar appen.
/// </para>
/// </summary>
/// <param name="Title">Kort rubrik. Det enda som säkert syns på en låsskärm.</param>
/// <param name="Body">En rad till. Får vara tom.</param>
/// <param name="Url">Relativ adress i appen som notisen öppnar, t.ex. <c>/match/{id}</c>.</param>
public sealed record PushMessage(string Title, string Body, string Url);

/// <summary>
/// En notis på väg ut: vad som ska sägas, och till vem.
///
/// <h3>Två mottagarkretsar</h3>
///
/// <para>
/// <b>Ett helt lag</b> — en matchändring eller ett nytt samåkningserbjudande når alla som
/// prenumererar på laget. <b>En handfull konton</b> — en åkförfrågan når föraren, ett svar
/// når den som frågade (`#63`). Exakt en av de två är satt; fabriksmetoderna ser till att
/// det inte går att blanda ihop.
/// </para>
/// </summary>
public sealed record PushDispatch
{
    private PushDispatch(Guid? teamId, IReadOnlyList<Guid>? accountIds, PushMessage message)
    {
        TeamId = teamId;
        AccountIds = accountIds;
        Message = message;
    }

    /// <summary>Satt när notisen går till ett helt lags prenumeranter.</summary>
    public Guid? TeamId { get; }

    /// <summary>Satt när notisen går till bestämda konton (deras alla enheter).</summary>
    public IReadOnlyList<Guid>? AccountIds { get; }

    public PushMessage Message { get; }

    /// <summary>Till alla som prenumererar på laget.</summary>
    public static PushDispatch ToTeam(Guid teamId, PushMessage message) =>
        new(teamId, null, message);

    /// <summary>Till bestämda konton — deras notiser om sin egen samåkning (§KM.12).</summary>
    public static PushDispatch ToAccounts(IReadOnlyList<Guid> accountIds, PushMessage message) =>
        new(null, accountIds, message);
}
