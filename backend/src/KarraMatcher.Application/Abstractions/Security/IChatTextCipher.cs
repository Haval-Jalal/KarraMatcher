namespace KarraMatcher.Application.Abstractions.Security;

/// <summary>
/// Krypterar chattens fritext i vila (§KM.10, `#redesign`).
///
/// <para>
/// Meddelandetext och anmälnings-motiveringar lagras krypterade i databasen, ovanpå den TLS som
/// redan skyddar transporten. Läcker en databasdump eller en SQL-injektion tabellen är texten
/// oläslig utan appens nyckel — "den kan inte komma ut". Det är <b>inte</b> end-to-end: servern
/// kan avkryptera, för truppens admin måste kunna läsa ett <em>anmält</em> meddelande och radera
/// det (§KM.7). Bytet mellan integritet och moderering är ett medvetet val (docs/PROJEKT-HANDOFF).
/// </para>
///
/// <para>
/// <see cref="Decrypt"/> är tålig: en text som inte bär krypterings-markören lämnas orörd, så
/// äldre klartext-rader (skrivna innan krypteringen infördes) fortsätter gå att läsa tills de
/// gallras. Tom sträng lagras som tom.
/// </para>
/// </summary>
public interface IChatTextCipher
{
    /// <summary>Klartext in, lagringsbar (krypterad) sträng ut.</summary>
    public string Encrypt(string plaintext);

    /// <summary>Lagrad sträng in, klartext ut. Okrypterad (äldre) text returneras oförändrad.</summary>
    public string Decrypt(string stored);
}
