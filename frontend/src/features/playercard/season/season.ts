import { isFilledIn, totalsFor, type Totals } from '../badges/badges'
import type { MatchReport, PlayerCardData } from '../storage/schema'

/**
 * Säsongen sammanfattad — barnets egen sida, inte ett kalkylark.
 *
 * <h3>Bara det familjen själv skrivit</h3>
 *
 * Allt här räknas ur rapporterna på enheten. Ingenting hämtas, ingenting slås upp, och
 * ingenting skickas (§KM.2). Det gör också att sidan går att läsa på ett flygplan, på en
 * telefon utan valt lag, och för en familj som importerat sin säsong från den gamla appen.
 */

/** Lagets facit, räknat ur de matcher familjen fyllt i ett resultat för. */
export interface TeamRecord {
  wins: number
  draws: number
  losses: number
}

/** En rad i barnets matchlista. */
export interface SeasonRow {
  id: string
  playedUtc: string
  /** Motståndaren, när rapporten minns den. Äldre rapporter bär bara datumet. */
  opponent: string | null
  goals: number
  assists: number
  teamGoals: number | null
  opponentGoals: number | null
}

/** Allt barnets sida visar. */
export interface Season {
  totals: Totals
  /** Mål och assist ihop. Det är så poäng räknas i lagsporter, och så barnet räknar. */
  points: number
  record: TeamRecord
  rows: SeasonRow[]
  /** Bästa matchen, när det finns en som sticker ut. */
  best: SeasonRow | null
}

export function seasonFor(card: PlayerCardData, childId: string): Season {
  const reports = card.reports
    .filter((report) => report.childId === childId && isFilledIn(report))
    .sort(byNewestFirst)

  const totals = totalsFor(card, childId)

  return {
    totals,
    points: totals.goals + totals.assists,
    record: recordFrom(reports),
    rows: reports.map(toRow),
    best: bestOf(reports),
  }
}

/**
 * Säsongen i klartext.
 *
 * <h3>Varför meningar och inte fler siffror</h3>
 *
 * Siffrorna står redan i totalerna ovanför. Det som gör sidan till ett samlarkort är att
 * någon <em>säger</em> vad de betyder — "åtta matcher, fem mål" är en säsong, medan
 * `8 · 5 · 3` är en rad i ett kalkylark.
 *
 * <para>
 * Varje mening bär bara det som faktiskt är ifyllt. Ett barn som inte gjort mål ska inte
 * läsa "0 mål" som en dom, och ett lag utan ifyllda resultat ska inte få ett påhittat
 * facit.
 * </para>
 */
export function summarise(season: Season, name: string): string[] {
  if (season.totals.matches === 0) {
    return []
  }

  const sentences: string[] = [`${name} har fyllt i ${matchWord(season.totals.matches)}.`]

  if (season.points > 0) {
    sentences.push(`Det har blivit ${scoreWords(season.totals.goals, season.totals.assists)}.`)
  }

  if (season.best !== null && season.best.goals >= 2) {
    // Bara nagot som sticker ut kallas basta matchen. Ett mal i en match dar barnet gjort
    // ett mal i alla ar ingen hojdpunkt, det ar sasongen.
    sentences.push(
      season.best.opponent === null
        ? `Bästa matchen: ${goalWord(season.best.goals)}.`
        : `Bästa matchen: ${goalWord(season.best.goals)} mot ${season.best.opponent}.`,
    )
  }

  const played = season.record.wins + season.record.draws + season.record.losses

  if (played > 0) {
    sentences.push(
      `Av de ${matchWord(played)} med ifyllt resultat vann laget ${String(season.record.wins)}, ` +
        `spelade ${String(season.record.draws)} oavgjort och förlorade ${String(season.record.losses)}.`,
    )
  }

  return sentences
}

function recordFrom(reports: MatchReport[]): TeamRecord {
  const record: TeamRecord = { wins: 0, draws: 0, losses: 0 }

  for (const report of reports) {
    // Bada malen maste vara ifyllda. Ett halvt resultat sager ingenting om utgangen, och
    // att lasa det tomma faltet som noll hade hittat pa en vinst.
    if (report.teamGoals === null || report.opponentGoals === null) {
      continue
    }

    if (report.teamGoals > report.opponentGoals) {
      record.wins += 1
    } else if (report.teamGoals < report.opponentGoals) {
      record.losses += 1
    } else {
      record.draws += 1
    }
  }

  return record
}

function bestOf(reports: MatchReport[]): SeasonRow | null {
  const best = reports.reduce<MatchReport | null>(
    (leader, report) => (leader === null || report.goals > leader.goals ? report : leader),
    null,
  )

  return best === null || best.goals === 0 ? null : toRow(best)
}

function toRow(report: MatchReport): SeasonRow {
  return {
    id: report.id,
    playedUtc: report.playedUtc,
    opponent: report.opponent,
    goals: report.goals,
    assists: report.assists,
    teamGoals: report.teamGoals,
    opponentGoals: report.opponentGoals,
  }
}

/** Senast spelade först — den matchen är den man vill se när sidan öppnas. */
function byNewestFirst(left: MatchReport, right: MatchReport): number {
  return right.playedUtc.localeCompare(left.playedUtc)
}

/**
 * Namnet i genitiv, på svenska.
 *
 * <para>
 * Ett namn som slutar på s, x eller z får inget extra s — "Elias matcher", inte "Eliass
 * matcher". Elias, Alex och Lukas är vanliga namn i den här åldersgruppen, så regeln
 * hade synts direkt på barnets egen sida.
 * </para>
 */
export function possessive(name: string): string {
  return /[sxz]$/i.test(name.trim()) ? name : `${name}s`
}

function matchWord(count: number): string {
  return `${String(count)} ${count === 1 ? 'match' : 'matcher'}`
}

function goalWord(count: number): string {
  return `${String(count)} mål`
}

function scoreWords(goals: number, assists: number): string {
  if (goals === 0) {
    return `${String(assists)} assist`
  }

  if (assists === 0) {
    return goalWord(goals)
  }

  return `${goalWord(goals)} och ${String(assists)} assist`
}
