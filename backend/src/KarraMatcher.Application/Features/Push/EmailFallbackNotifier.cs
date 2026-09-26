using KarraMatcher.Application.Abstractions.Email;
using KarraMatcher.Application.Abstractions.Persistence;
using KarraMatcher.Application.Abstractions.Push;
using KarraMatcher.Application.Features.Auth;

using Microsoft.Extensions.Options;

namespace KarraMatcher.Application.Features.Push;

/// <summary>
/// E-postfallback för kritiska notiser (opålitlig push är föräldrarnas vanligaste klagomål).
///
/// <h3>Bara det man inte får missa</h3>
///
/// <para>
/// Fallbacken gäller <b>kritiska</b> kategorier — en händelse som ändrats/ställts in och en ny
/// kallelse. Samåkning och chatt är inte den sortens besked man inte får missa, och ska inte
/// fylla en inkorg.
/// </para>
///
/// <h3>Äkta fallback</h3>
///
/// <para>
/// Mejlet går bara till dem som push <em>inte</em> når: en medlem som vill ha kategorin men
/// saknar en fungerande push-prenumeration. Har man en enhet får man push och inget mejl —
/// ingen dubbelnotis. Den som stängt av kategorin nås inte alls.
/// </para>
///
/// <h3>Samma tak som push</h3>
///
/// <para>
/// Mejlet bär bara notisens neutrala rubrik och rad — aldrig ett barns namn, aldrig fritext
/// (§KM.1/§KM.10) — plus en länk in i appen. Ett leveransfel fäller aldrig utskicket
/// (<see cref="IEmailSender"/> kastar inte), och adresser loggas aldrig.
/// </para>
/// </summary>
public sealed class EmailFallbackNotifier(
    IEmailFallbackRepository recipients,
    IEmailSender email,
    IOptions<AuthOptions> options)
{
    public async Task SendAsync(PushDispatch dispatch, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(dispatch);

        if (!IsCritical(dispatch.Category))
        {
            return;
        }

        var targets = dispatch.AccountIds is null
            ? await recipients
                .ListForTeamAsync(dispatch.TeamId, dispatch.Category, cancellationToken)
                .ConfigureAwait(false)
            : dispatch.AccountIds is { Count: > 0 } accounts
                ? await recipients
                    .ListForAccountsAsync(dispatch.TeamId, accounts, dispatch.Category, cancellationToken)
                    .ConfigureAwait(false)
                : [];

        if (targets.Count == 0)
        {
            return;
        }

        var body = BuildBody(dispatch.Message);

        foreach (var target in targets)
        {
            await email
                .SendAsync(target.Email, dispatch.Message.Title, body, cancellationToken)
                .ConfigureAwait(false);
        }
    }

    /// <summary>Ett besked man inte får missa: en ändrad/inställd händelse eller en ny kallelse.</summary>
    private static bool IsCritical(PushCategory category) =>
        category is PushCategory.EventChange or PushCategory.Kallelse;

    private string BuildBody(PushMessage message)
    {
        var link = $"{options.Value.AppBaseUrl.TrimEnd('/')}{message.Url}";

        return string.IsNullOrEmpty(message.Body)
            ? link
            : $"{message.Body}{Environment.NewLine}{Environment.NewLine}{link}";
    }
}
