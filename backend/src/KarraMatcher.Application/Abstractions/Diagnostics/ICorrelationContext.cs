namespace KarraMatcher.Application.Abstractions.Diagnostics;

/// <summary>
/// Ger den pågående requestens correlation-id (§KM.10) till lager som inte känner till HTTP.
///
/// <para>
/// Audit-loggen behöver id:t för att en post ska gå att koppla till requestens loggrader,
/// men Application- och Infrastructure-lagren ska inte veta att det kommer från en
/// <c>HttpContext</c>. Implementationen bor i Api-lagret; här finns bara behovet.
/// </para>
/// </summary>
public interface ICorrelationContext
{
    /// <summary>Requestens correlation-id, eller <c>null</c> utanför en request.</summary>
    public string? CorrelationId { get; }
}
