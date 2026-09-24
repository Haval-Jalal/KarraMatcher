/**
 * Gör en läsbar slug av ett namn (`#257`).
 *
 * <para>
 * En slug är ett tekniskt begrepp en admin inte ska behöva förstå. I stället föreslås den
 * ur namnet medan man skriver, och den går att ändra för hand om man vill. Resultatet följer
 * samma regel som valideringen kräver: små `a–z`, siffror och bindestreck.
 * </para>
 *
 * <para>
 * Svenska tecken translittereras via Unicode-normalisering: `å`/`ä` → `a`, `ö` → `o`. `NFD`
 * delar bokstaven i grundtecken + accent, och accenten (en icke-utrymmande markering,
 * `\p{Mn}`) tas bort. "Kärra IF" blir alltså `karra-if`.
 * </para>
 */
export function slugify(name: string): string {
  return name
    .normalize('NFD')
    .replace(/\p{Mn}/gu, '')
    .toLowerCase()
    .replace(/[^a-z0-9]+/g, '-')
    .replace(/^-+|-+$/g, '')
}
