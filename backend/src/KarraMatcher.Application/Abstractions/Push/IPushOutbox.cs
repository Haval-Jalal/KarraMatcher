using KarraMatcher.Application.Features.Push;

namespace KarraMatcher.Application.Abstractions.Push;

/// <summary>
/// Utkorgen: notiser på väg ut (`#61`).
///
/// <para>
/// Namnet är "utkorg" och inte "kö" av två skäl. Analysreglerna reserverar <c>Queue</c> som
/// suffix för samlingstyper, och utkorg beskriver bättre vad det är: ett ställe man lägger
/// något och går vidare, utan att veta när det hämtas.
/// </para>
///
/// <para>
/// <b>Ingen handler skickar själv.</b> En tränare som flyttar en match ska få sitt svar
/// direkt, inte efter att femton notiser gått iväg över internet — och ett push-utskick som
/// hänger sig får aldrig hålla en request öppen. Handlern lägger alltså bara till i kön och
/// går vidare.
/// </para>
///
/// <para>
/// Kön är avsiktligt enkel: en kanal i processen, tömd av en bakgrundstjänst. Det räcker för
/// ett lag med några hundra föräldrar, och alternativet — en riktig meddelandekö — hade
/// varit en till tjänst att drifta och betala för. Priset är att en notis kan gå förlorad om
/// processen dör i samma sekund. Det är acceptabelt: notisen är komplementet, kalenderfeeden
/// är den primära kanalen (§KM.0 A5).
/// </para>
/// </summary>
public interface IPushOutbox
{
    /// <summary>Ställer en notis i kö. Returnerar direkt.</summary>
    public void Enqueue(PushDispatch dispatch);
}

/// <summary>
/// Läsaren: bakgrundstjänsten som tömmer utkorgen.
///
/// <para>
/// Skild från <see cref="IPushOutbox"/> med flit. Den som köar ska inte kunna läsa, och
/// den som läser ska inte behöva se hur något köas — uppdelningen gör det synligt i
/// signaturen vem som är vem. Samma klass ligger bakom båda.
/// </para>
/// </summary>
public interface IPushOutboxReader
{
    public IAsyncEnumerable<PushDispatch> ReadAllAsync(CancellationToken cancellationToken);
}
