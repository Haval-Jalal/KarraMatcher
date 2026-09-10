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
/// En notis på väg ut: vad som ska sägas, och till vilket lags prenumeranter.
/// </summary>
public sealed record PushDispatch(Guid TeamId, PushMessage Message);
