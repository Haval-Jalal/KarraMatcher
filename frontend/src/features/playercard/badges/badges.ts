import type { MatchReport, PlayerCardData } from '../storage/schema'

/**
 * Märkena, räknade ur den lokala statistiken.
 *
 * <h3>Varför de ser ut precis så här</h3>
 *
 * De sex märkena är <b>avlästa ur föregångaren</b> (`index 4.html`), inte påhittade — samma
 * emoji, samma namn, samma trösklar. Familjerna har redan en säsong bakom sig, och ett barn
 * som låst upp Hattrick ska inte behöva göra om det för att vi tyckte att fyra mål lät
 * bättre. Tonen fanns redan; den här filen flyttar den.
 *
 * <h3>Ingenting av det här lämnar telefonen</h3>
 *
 * Märken räknas fram ur kortet varje gång de visas. De sparas inte som resultat, bara som
 * <em>sedda</em> — och statistiken de räknas ur når aldrig servern (§KM.2).
 */

/** Ett märke att låsa upp. */
export interface Badge {
  id: string
  emoji: string
  name: string
  /** Vad som krävs, i klartext. Visas även när märket är låst. */
  requirement: string
  /** Hur långt barnet kommit, och vad som krävs. Driver både text och mätare. */
  progress: (totals: Totals) => { done: number; needed: number }
}

/** Det märkena räknas ur. */
export interface Totals {
  goals: number
  assists: number
  /** Flest mål i en och samma match. Hattricket bor här. */
  mostGoalsInAMatch: number
  matches: number
}

/**
 * De sex märkena, i den ordning föregångaren visade dem.
 *
 * <para>
 * Ordningen är inte alfabetisk och inte efter svårighet — den är den familjerna känner
 * igen. Att flytta om dem hade gjort listan främmande utan att göra den bättre.
 * </para>
 */
export const BADGES: Badge[] = [
  {
    id: 'forsta-malet',
    emoji: '⚽',
    name: 'Första målet',
    requirement: 'Gör ett mål',
    progress: (totals) => ({ done: totals.goals, needed: 1 }),
  },
  {
    id: 'passningskung',
    emoji: '🎯',
    name: 'Passningskung',
    requirement: '5 assist',
    progress: (totals) => ({ done: totals.assists, needed: 5 }),
  },
  {
    id: 'hattrick',
    emoji: '🎩',
    name: 'Hattrick',
    requirement: '3 mål i en match',
    progress: (totals) => ({ done: totals.mostGoalsInAMatch, needed: 3 }),
  },
  {
    id: 'malmaskin',
    emoji: '🔥',
    name: 'Målmaskin',
    requirement: '10 mål',
    progress: (totals) => ({ done: totals.goals, needed: 10 }),
  },
  {
    id: 'poangstjarna',
    emoji: '⭐',
    name: 'Poängstjärna',
    requirement: '10 mål och assist ihop',
    progress: (totals) => ({ done: totals.goals + totals.assists, needed: 10 }),
  },
  {
    id: 'stammis',
    emoji: '🏟️',
    name: 'Stammis',
    requirement: '10 matcher',
    progress: (totals) => ({ done: totals.matches, needed: 10 }),
  },
]

/**
 * Räknar ihop ett barns säsong.
 *
 * <para>
 * En match räknas som spelad när rapporten <em>innehåller något</em>. En tom rapport blir
 * till så fort någon rör en knapp och ångrar sig, och den ska inte räknas som en match —
 * Stammis ska betyda tio matcher, inte tio felklick.
 * </para>
 */
export function totalsFor(card: PlayerCardData, childId: string): Totals {
  const reports = card.reports.filter((report) => report.childId === childId && hasContent(report))

  return {
    goals: sum(reports, (report) => report.goals),
    assists: sum(reports, (report) => report.assists),
    mostGoalsInAMatch: reports.reduce((most, report) => Math.max(most, report.goals), 0),
    matches: reports.length,
  }
}

/** Märkena barnet har låst upp. */
export function earnedBadges(totals: Totals): Badge[] {
  return BADGES.filter((badge) => isEarned(badge, totals))
}

/** Sant när märket är upplåst. */
export function isEarned(badge: Badge, totals: Totals): boolean {
  const { done, needed } = badge.progress(totals)

  return done >= needed
}

/**
 * Märken barnet låst upp men aldrig fått se firas.
 *
 * <para>
 * Det är den här listan som gör firandet till en händelse i stället för en påminnelse:
 * ett märke firas en gång, den gång det låstes upp.
 * </para>
 */
export function unseenBadges(card: PlayerCardData, childId: string): Badge[] {
  const child = card.children.find((candidate) => candidate.id === childId)

  if (child === undefined) {
    return []
  }

  return earnedBadges(totalsFor(card, childId)).filter(
    (badge) => !child.seenBadges.includes(badge.id),
  )
}

/**
 * Markerar allt barnet redan förtjänat som sett.
 *
 * <para>
 * Används på två ställen som ser likadana ut för familjen: när ett gammalt kort migreras,
 * och när en kod från den gamla appen importeras. I båda fallen kommer en hel säsong in på
 * en gång, och sex firanden i rad firar ingenting som just hänt.
 * </para>
 */
export function markEarnedAsSeen(card: PlayerCardData): PlayerCardData {
  return {
    ...card,
    children: card.children.map((child) => ({
      ...child,
      seenBadges: earnedBadges(totalsFor(card, child.id)).map((badge) => badge.id),
    })),
  }
}

/**
 * En rapport räknas som ifylld när den bär något familjen faktiskt skrivit in.
 *
 * <para>
 * Resultatet räknas med. Det skrivs på varje syskons rapport när någon fyller i hur
 * matchen slutade, precis som i föregångaren — där en match räknades som spelad så fort
 * den hade ett resultat, oavsett om barnet gjort mål.
 * </para>
 */
function hasContent(report: MatchReport): boolean {
  return (
    report.goals > 0 ||
    report.assists > 0 ||
    report.teamGoals !== null ||
    report.opponentGoals !== null ||
    (report.note !== null && report.note.trim() !== '')
  )
}

function sum(reports: MatchReport[], pick: (report: MatchReport) => number): number {
  return reports.reduce((total, report) => total + pick(report), 0)
}
