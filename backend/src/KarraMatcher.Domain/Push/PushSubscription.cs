namespace KarraMatcher.Domain.Push;

/// <summary>
/// En webbläsare som vill ha notiser om ett lags matcher (`#60`).
///
/// <h3>Ingen inloggning, och ingen koppling till ett konto</h3>
///
/// <para>
/// Prenumerationen hör till en <em>webbläsare</em>, inte till ett konto. Det är avsiktligt
/// (§KM.3, M7): kräver notiser ett konto når de en bråkdel av föräldrarna, och en
/// matchändring är precis det slags upplysning som ska nå alla.
/// </para>
///
/// <para>
/// Det finns därför <b>inget kontofält här</b>, inte ens ett valfritt. Samåkningsnotiserna
/// i <c>#63</c> kommer att behöva veta vilken enhet som hör till vem, och då införs
/// kopplingen med den avvägning den kräver: §KM.6 säger att allt som ägs av ett konto ska
/// försvinna med det, och en prenumeration som raderas när någon raderar sitt konto tar
/// tyst bort besked om att lördagens match är inställd. Den avvägningen hör hemma i det
/// issuet, inte här — och en kolumn som inte används ännu är en kolumn som inte ska finnas.
/// </para>
///
/// <h3>Adressen är en personuppgift</h3>
///
/// <para>
/// <see cref="Endpoint"/> är en unik URL till webbläsarens push-tjänst. Den identifierar
/// en enskild enhet lika bra som ett telefonnummer och räknas som personuppgift: den
/// loggas aldrig (§KM.10), lämnas aldrig ut i något svar, och försvinner när någon
/// avregistrerar sig eller när tjänsten svarar att den är död (`#61`).
/// </para>
///
/// <h3>Nycklarna är webbläsarens, inte våra</h3>
///
/// <para>
/// <see cref="P256dh"/> och <see cref="Auth"/> kommer från webbläsaren och används för att
/// kryptera nyttolasten så att push-tjänsten inte kan läsa den. De är alltså det som gör
/// att en notis kan passera Google eller Apple utan att de ser innehållet.
/// </para>
/// </summary>
public sealed class PushSubscription
{
    public Guid Id { get; set; }

    /// <summary>Laget prenumerationen gäller. En enhet kan prenumerera på flera lag.</summary>
    public Guid TeamId { get; set; }

    /// <summary>Push-tjänstens adress till just den här webbläsaren. Personuppgift.</summary>
    public required string Endpoint { get; set; }

    /// <summary>Webbläsarens publika nyckel, för kryptering av nyttolasten.</summary>
    public required string P256dh { get; set; }

    /// <summary>Webbläsarens autentiseringshemlighet, för kryptering av nyttolasten.</summary>
    public required string Auth { get; set; }

    public DateTime CreatedUtc { get; set; }

    /// <summary>
    /// Senast lyckade utskick.
    ///
    /// <para>
    /// Används av gallringen i <c>#61</c>: en prenumeration vars push-tjänst svarat att
    /// den är borta ska raderas, inte försökas igen i all evighet.
    /// </para>
    /// </summary>
    public DateTime? LastUsedUtc { get; set; }
}
