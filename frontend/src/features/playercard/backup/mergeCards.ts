import type { PlayerCardData } from '../storage/schema'

/**
 * Slår ihop ett inläst kort med det som redan finns på enheten.
 *
 * <h3>Ingenting som finns lokalt går förlorat</h3>
 *
 * Import <b>lägger till</b>, den skriver aldrig över. Två vårdnadshavare med varsin telefon
 * har varsin uppsättning statistik — det följer av modellen (§KM.2) — och den som importerar
 * den andres kod ska inte förlora sin egen.
 *
 * Vid krock vinner därför det lokala. Telefonen man står med är den man valt att återställa
 * <em>till</em>, och att skriva över dess data hade varit det enda ohjälpliga misstaget i
 * hela funktionen: koden går att importera om, men det överskrivna finns ingen annanstans.
 *
 * <h3>Samma regel som föregångaren</h3>
 *
 * Den gamla appen gjorde likadant med barnen (`if (!kids.some(x => x.id === k.id))`). Att
 * behålla beteendet betyder att en familj som flyttat fram och tillbaka mellan apparna får
 * samma utfall som de är vana vid.
 */
export function mergeCards(local: PlayerCardData, incoming: PlayerCardData): PlayerCardData {
  const knownChildren = new Set(local.children.map((child) => child.id))
  const knownReports = new Set(local.reports.map((report) => report.id))

  return {
    ...local,
    children: [
      ...local.children,
      ...incoming.children.filter((child) => !knownChildren.has(child.id)),
    ],
    reports: [
      ...local.reports,
      ...incoming.reports.filter((report) => !knownReports.has(report.id)),
    ],
  }
}

/** Vad en genomförd import tillförde. Visas för den som importerade. */
export function describeMerge(before: PlayerCardData, after: PlayerCardData): string {
  const children = after.children.length - before.children.length
  const reports = after.reports.length - before.reports.length

  if (children === 0 && reports === 0) {
    return 'Allt i koden fanns redan här. Ingenting ändrades.'
  }

  const parts: string[] = []

  if (children > 0) {
    parts.push(`${String(children)} ${children === 1 ? 'barn' : 'barn'}`)
  }

  if (reports > 0) {
    parts.push(`${String(reports)} ${reports === 1 ? 'matchrapport' : 'matchrapporter'}`)
  }

  return `Lade till ${parts.join(' och ')}. Det som redan fanns här är orört.`
}
