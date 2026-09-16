namespace KarraMatcher.Domain.Children;

/// <summary>
/// Kopplingen mellan en vårdnadshavare (ett konto) och ett barn (v2, `#190`).
///
/// <para>
/// Många-till-många: ett barn kan ha flera vårdnadshavare (varsin telefon), och en
/// vårdnadshavare flera barn. Kopplingen är själva "medlemskapet" som gör att en förälder ser
/// sitt barns trupp. Samtycket (§KM.6) knyts till den här kopplingen i `#195`; här finns bara
/// själva relationen och när den skapades.
/// </para>
/// </summary>
public sealed class Guardianship
{
    public Guid Id { get; set; }

    public Guid AccountId { get; set; }

    public Accounts.Account? Account { get; set; }

    public Guid ChildId { get; set; }

    public Child? Child { get; set; }

    public DateTime GrantedUtc { get; set; }
}
