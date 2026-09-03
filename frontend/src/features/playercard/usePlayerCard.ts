import { useCallback, useState } from 'react'

import { readCard, writeCard } from './storage/playerCardStore'
import type { Child, PlayerCardData } from './storage/schema'

/**
 * Spelarkortet som React-tillstånd.
 *
 * <h3>Varför ingen TanStack Query här</h3>
 *
 * Query är för <em>server</em>-tillstånd — cache, omhämtning, invalidering. Kortet finns
 * bara på den här enheten och kan inte ändras av någon annan, så det finns ingenting att
 * synkronisera. Att lägga ett cachelager över localStorage vore att lösa ett problem som
 * inte finns, och att antyda att datan kommer någonstans ifrån.
 *
 * <h3>Skrivningen kan misslyckas</h3>
 *
 * Full lagring är sällsynt men inte omöjlig. Varje ändring svarar därför med om den
 * sparades, så en förälder som fyllt i något får veta att det inte gick — i stället för
 * att upptäcka det nästa säsong.
 */
export function usePlayerCard() {
  const [card, setCard] = useState<PlayerCardData>(readCard)

  const save = useCallback((next: PlayerCardData): boolean => {
    const saved = writeCard(next)

    // Tillståndet uppdateras även när skrivningen inte gick igenom: det som står på
    // skärmen ska vara det användaren just gjorde, med ett besked om att det inte sparades.
    setCard(next)

    return saved
  }, [])

  /**
   * Lägger till ett barn.
   *
   * <para>
   * Formuläret frågar inte efter <c>seenBadges</c> och ska inte behöva veta att fältet
   * finns. Ett nytt barn har inte sett några märken — det är hookens sak att veta, inte
   * anropsställets.
   * </para>
   */
  const addChild = useCallback(
    (child: Omit<Child, 'id' | 'seenBadges'>): boolean =>
      save({
        ...card,
        children: [...card.children, { ...child, id: newId(), seenBadges: [] }],
      }),
    [card, save],
  )

  const updateChild = useCallback(
    (id: string, changes: Partial<Omit<Child, 'id'>>): boolean =>
      save({
        ...card,
        children: card.children.map((existing) =>
          existing.id === id ? { ...existing, ...changes } : existing,
        ),
      }),
    [card, save],
  )

  /**
   * Tar bort ett barn och allt som hör till det.
   *
   * <para>
   * Matchrapporterna följer med. En rapport utan barn syns ingenstans men ligger kvar i
   * lagringen — och när den familjen en dag exporterar sin säkerhetskopia följer den med
   * dit också. Att radera ska betyda radera (§KM.6).
   * </para>
   */
  const removeChild = useCallback(
    (id: string): boolean =>
      save({
        ...card,
        children: card.children.filter((existing) => existing.id !== id),
        reports: card.reports.filter((report) => report.childId !== id),
      }),
    [card, save],
  )

  /**
   * Läser om kortet från enheten.
   *
   * Behövs efter en import, som skriver till lagringen utan att gå genom den här hooken.
   * Alternativet vore att låta importen returnera det nya kortet — men då hade två ställen
   * ägt sanningen, och lagringen är den enda som faktiskt gör det.
   */
  const reload = useCallback(() => {
    setCard(readCard())
  }, [])

  return { card, addChild, updateChild, removeChild, reload }
}

/**
 * Ett id som bara behöver vara unikt på den här enheten.
 *
 * `crypto.randomUUID` när den finns, annars ett värde som duger lika bra: id:t lämnar
 * aldrig telefonen och jämförs aldrig med något annat än sig självt.
 */
function newId(): string {
  return globalThis.crypto?.randomUUID?.() ?? `barn-${String(Date.now())}-${String(Math.random())}`
}
