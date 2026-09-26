namespace KarraMatcher.Domain.Calendar;

/// <summary>
/// En privat kalender-nyckel för ett konto (`#307`-uppföljning, kalender bakom medlemskap).
///
/// <h3>Nyckeln är adressen</h3>
///
/// <para>
/// En kalender-app kan inte skicka en inloggning; den hämtar bara en URL med jämna mellanrum.
/// Därför bär URL:en en ogissbar nyckel (256 bitar) som pekar på exakt <em>ett</em> konto, och
/// feeden bakom den visar bara det kontots lag-scheman — aldrig något om ett barn (§KM.1). Ingen
/// publik feed återinförs (§KM.4): nyckeln är personlig och kan återkallas.
/// </para>
///
/// <h3>Lagras i klartext, med flit</h3>
///
/// <para>
/// Till skillnad från en session-token lagras den här som den är, så att kontot kan se och
/// kopiera sin länk när som helst. Avvägningen är medveten: nyckeln ger läsrätt till matchtider
/// utan personuppgifter, och kan återkallas. Den ägs av kontot och försvinner med det (§KM.6).
/// </para>
/// </summary>
public sealed class CalendarToken
{
    public Guid Id { get; set; }

    public Guid AccountId { get; set; }

    /// <summary>Den ogissbara nyckeln i kalender-URL:en. Unik.</summary>
    public required string Token { get; set; }

    public DateTime CreatedUtc { get; set; }

    /// <summary>Senaste gången en kalender-app hämtade feeden. Inget krav, bara en upplysning.</summary>
    public DateTime? LastUsedUtc { get; set; }
}
