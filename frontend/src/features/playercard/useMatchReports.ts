import { useCallback, useState } from 'react'

import { unseenBadges } from './badges/badges'
import { readCard, writeCard } from './storage/playerCardStore'
import type { MatchReport } from './storage/schema'

/**
 * Matchrapporterna för en match.
 *
 * <h3>Sparas direkt, utan sparaknapp</h3>
 *
 * Varje tryck skriver till enheten. En sparaknapp är ett sätt att förlora data: rapporten
 * fylls i med ett barn bredvid sig, ofta på väg ut ur en bil, och det som skrivs men
 * aldrig sparas är borta utan att någon märker det.
 *
 * <h3>Aldrig negativa</h3>
 *
 * Ett negativt antal mål betyder ingenting. Spärren sitter både här och på knappen — i
 * gränssnittet så att den syns, och här så att den gäller.
 */
export function useMatchReports(matchId: string, opponent: string | null = null) {
  const [card, setCard] = useState(readCard)

  const reportFor = useCallback(
    (childId: string): MatchReport =>
      card.reports.find((report) => report.matchId === matchId && report.childId === childId) ??
      emptyReport(matchId, childId, opponent),
    [card.reports, matchId, opponent],
  )

  const save = useCallback(
    (childId: string, change: (report: MatchReport) => MatchReport) => {
      const current = readCard()
      const existing = current.reports.find(
        (report) => report.matchId === matchId && report.childId === childId,
      )

      /*
       * Motstandaren skrivs av vid varje sparning, inte bara nar rapporten skapas. En
       * rapport som fylldes i pa version 3 saknar namnet, och nasta trycket ar den
       * naturliga stunden att fylla det -- familjen star anda i ratt match.
       */
      const updated = { ...change(existing ?? emptyReport(matchId, childId, opponent)) }

      updated.opponent ??= opponent

      const next = {
        ...current,
        reports:
          existing === undefined
            ? [...current.reports, updated]
            : current.reports.map((report) => (report.id === existing.id ? updated : report)),
      }

      writeCard(next)
      setCard(next)
    },
    [matchId, opponent],
  )

  const adjust = useCallback(
    (childId: string, field: 'goals' | 'assists', delta: number) => {
      save(childId, (report) => ({
        ...report,
        // Math.max och inte en if: spärren ska gälla oavsett hur den anropas.
        [field]: Math.max(0, report[field] + delta),
      }))
    },
    [save],
  )

  /**
   * Resultatet gäller matchen, inte ett enskilt barn.
   *
   * <para>
   * Det skrivs ändå på varje syskons rapport, så att en rapport är fullständig i sig
   * själv. Tas ett barn bort ska den andra syskonets rapport fortfarande veta hur matchen
   * slutade.
   * </para>
   */
  const setResult = useCallback(
    (field: 'teamGoals' | 'opponentGoals', delta: number) => {
      const current = readCard()

      for (const child of current.children) {
        save(child.id, (report) => ({
          ...report,
          [field]: Math.max(0, (report[field] ?? 0) + delta),
        }))
      }
    },
    [save],
  )

  /**
   * Markerar de firade märkena som sedda.
   *
   * <para>
   * Ligger här därför att hooken redan äger kortet. Att låta firandet ha ett eget
   * tillstånd hade gett två ägare till samma data, och den som fyller i ett mål medan
   * firandet står på skärmen hade riskerat att få det överskrivet.
   * </para>
   */
  const acknowledgeBadges = useCallback(() => {
    const current = readCard()

    const next = {
      ...current,
      children: current.children.map((child) => ({
        ...child,
        seenBadges: [
          ...child.seenBadges,
          ...unseenBadges(current, child.id).map((badge) => badge.id),
        ],
      })),
    }

    writeCard(next)
    setCard(next)
  }, [])

  return { card, reportFor, adjust, setResult, acknowledgeBadges }
}

function emptyReport(matchId: string, childId: string, opponent: string | null): MatchReport {
  return {
    id: `${matchId}-${childId}`,
    childId,
    matchId,
    playedUtc: new Date().toISOString(),
    goals: 0,
    assists: 0,
    teamGoals: null,
    opponentGoals: null,
    opponent,
    note: null,
  }
}
