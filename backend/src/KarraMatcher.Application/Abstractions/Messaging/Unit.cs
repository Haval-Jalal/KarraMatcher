namespace KarraMatcher.Application.Abstractions.Messaging;

/// <summary>
/// "Ingenting", som resultattyp.
///
/// <para>
/// Ett kommando som inte har något att svara med behöver ändå en resultattyp, eftersom
/// dispatchern är byggd kring <c>Task&lt;TResult&gt;</c>. Alternativet vore en andra
/// uppsättning interfaces för kommandon utan svar — dubbelt så mycket rör för att slippa
/// en tom typ.
/// </para>
/// </summary>
public sealed record Unit
{
    public static readonly Unit Value = new();

    private Unit()
    {
    }
}
