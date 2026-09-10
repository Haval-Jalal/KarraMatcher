using System.Threading.Channels;

using KarraMatcher.Application.Abstractions.Push;
using KarraMatcher.Application.Features.Push;

namespace KarraMatcher.Infrastructure.Push;

/// <summary>
/// Kön av notiser på väg ut — en kanal i processen (`#61`).
///
/// <h3>Varför den är obegränsad, men ändå inte farlig</h3>
///
/// <para>
/// En begränsad kanal måste antingen blockera skrivaren eller slänga något. Att blockera
/// vore att flytta väntetiden tillbaka in i tränarens request, vilket är precis vad kön
/// finns för att undvika. Att slänga tyst vore värre.
/// </para>
///
/// <para>
/// Risken med obegränsat är att minnet fylls. Den är teoretisk här: notiser skapas av
/// mänskliga handlingar — en flyttad match, ett nytt samåkningserbjudande — några gånger om
/// dagen. Skulle det någon gång bli en verklig risk är det ett tecken på att kön ska ligga
/// utanför processen, inte på att den ska bli mindre.
/// </para>
/// </summary>
internal sealed class PushOutbox : IPushOutbox, IPushOutboxReader
{
    private readonly Channel<PushDispatch> _channel =
        Channel.CreateUnbounded<PushDispatch>(new UnboundedChannelOptions
        {
            // En lasare: bakgrundstjansten. Kanalen slipper da synkronisering den inte behover.
            SingleReader = true,
        });

    public void Enqueue(PushDispatch dispatch)
    {
        ArgumentNullException.ThrowIfNull(dispatch);

        // TryWrite och inte WriteAsync: en obegransad kanal tar alltid emot, sa det finns
        // ingenting att vanta pa -- och den som koar ska inte behova vara asynkron.
        _channel.Writer.TryWrite(dispatch);
    }

    /// <summary>Läses av bakgrundstjänsten. Blockerar tills något finns att göra.</summary>
    public IAsyncEnumerable<PushDispatch> ReadAllAsync(CancellationToken cancellationToken) =>
        _channel.Reader.ReadAllAsync(cancellationToken);
}
