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
/// <param name="Body">En rad till. Får vara tom. Bärs av BÅDE pushen och e-postfallbacken, så den
/// ska vara neutral — aldrig barn-PII eller fritext (§KM.1/§KM.10).</param>
/// <param name="Url">Relativ adress i appen som notisen öppnar, t.ex. <c>/handelse/{id}</c>.</param>
/// <param name="Detail">Extra rad som läggs till <b>enbart i pushen</b>, aldrig i e-postfallbacken.
/// Hit hör sådant ägaren valt att framföra i pushen men som inte ska ut i klartext till en
/// tredjeparts-e-posttjänst, t.ex. adminens kallelse-notis (`#468`/#582). Null = ingen extra rad.</param>
public sealed record PushMessage(string Title, string Body, string Url, string? Detail = null);

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
    private PushDispatch(
        Guid? teamId,
        Guid? ageGroupId,
        IReadOnlyList<Guid>? accountIds,
        PushCategory category,
        PushMessage message)
    {
        TeamId = teamId;
        AgeGroupId = ageGroupId;
        AccountIds = accountIds;
        Category = category;
        Message = message;
    }

    /// <summary>Laget notisen når (<c>ToTeam</c>), annars null.</summary>
    public Guid? TeamId { get; }

    /// <summary>Truppen notisen når (<c>ToTrupp</c>, en trupp-vid händelse `#332`), annars null.</summary>
    public Guid? AgeGroupId { get; }

    /// <summary>Bestämda konton (deras alla enheter) för <c>ToAccounts</c>, annars null.</summary>
    public IReadOnlyList<Guid>? AccountIds { get; }

    /// <summary>Vilket slags notis. Styr inte längre vem (global på/av), men avgör "kritisk" för mejl.</summary>
    public PushCategory Category { get; }

    public PushMessage Message { get; }

    /// <summary>Till lagets medlemmar (som har notiser på).</summary>
    public static PushDispatch ToTeam(Guid teamId, PushCategory category, PushMessage message) =>
        new(teamId, null, null, category, message);

    /// <summary>Till hela truppens medlemmar — för en trupp-vid händelse (`#332`).</summary>
    public static PushDispatch ToTrupp(Guid ageGroupId, PushCategory category, PushMessage message) =>
        new(null, ageGroupId, null, category, message);

    /// <summary>Till bestämda konton (deras alla enheter), oavsett lag eller trupp.</summary>
    public static PushDispatch ToAccounts(
        IReadOnlyList<Guid> accountIds,
        PushCategory category,
        PushMessage message) =>
        new(null, null, accountIds, category, message);
}
