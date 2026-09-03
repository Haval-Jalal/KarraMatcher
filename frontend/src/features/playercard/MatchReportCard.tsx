import type { Match } from '@/features/matches'

import { BadgeCelebration } from './badges/BadgeCelebration'
import type { Child, MatchReport } from './storage/schema'
import { useMatchReports } from './useMatchReports'

/**
 * Matchrapporten, ifylld efter matchen.
 *
 * <h3>Vad det här ska vara</h3>
 *
 * En liten stund tillsammans efter matchen — inte en rapporteringsplikt. Därför inga
 * obligatoriska fält, ingen sparaknapp och ingenting som måste fyllas i innan man får
 * stänga. Det som fylls i sparas direkt; det som lämnas tomt förblir tomt.
 *
 * <h3>Varför ingen sparaknapp</h3>
 *
 * En sparaknapp är ett sätt att förlora data. Den fylls i med ett barn bredvid sig, ofta
 * på väg ut ur en bil — och det som skrivs men aldrig sparas är borta utan att någon
 * märker det.
 *
 * <h3>Ingenting av det här lämnar telefonen</h3>
 *
 * §KM.2. Komponenten når lagringen, aldrig API-lagret.
 */
export function MatchReportCard({ match, children }: { match: Match; children: Child[] }) {
  const { card, reportFor, adjust, setResult, acknowledgeBadges } = useMatchReports(
    match.id,
    match.opponent,
  )

  if (match.status === 'Cancelled') {
    /*
     * En instalid match spelades aldrig. Ett inmatningsfalt dar hade bjudit in till att
     * fylla i nagot som inte hant, och en nolla i statistiken ar samre an ingen rad alls.
     */
    return null
  }

  if (children.length === 0) {
    return null
  }

  return (
    <section className="report">
      <h2>Efter matchen</h2>

      <p className="state">
        Fyll i tillsammans. Allt sparas direkt på den här telefonen — ingenting skickas någonstans,
        och ingen annan ser det.
      </p>

      <div className="report__result">
        <span className="report__label" id="resultat-etikett">
          Resultat
        </span>

        <div className="report__scores" role="group" aria-labelledby="resultat-etikett">
          <Stepper
            label="Våra mål"
            value={reportFor(children[0]!.id).teamGoals ?? 0}
            onChange={(delta) => {
              setResult('teamGoals', delta)
            }}
          />
          <Stepper
            label="Deras mål"
            value={reportFor(children[0]!.id).opponentGoals ?? 0}
            onChange={(delta) => {
              setResult('opponentGoals', delta)
            }}
          />
        </div>
      </div>

      {children.map((child) => (
        <div key={child.id} className="report__child">
          <h3>{child.name}</h3>

          <div className="report__scores">
            <Stepper
              label={`Mål — ${child.name}`}
              shortLabel="Mål"
              value={reportFor(child.id).goals}
              onChange={(delta) => {
                adjust(child.id, 'goals', delta)
              }}
            />
            <Stepper
              label={`Assist — ${child.name}`}
              shortLabel="Assist"
              value={reportFor(child.id).assists}
              onChange={(delta) => {
                adjust(child.id, 'assists', delta)
              }}
            />
          </div>
        </div>
      ))}

      {/*
        Firandet star sist: markena lases upp av det som fylls i ovanfor, och ska dyka upp
        under handen som just tryckte -- inte ovanfor den och knuffa undan raden man hall
        pa med.
      */}
      <BadgeCelebration card={card} children={children} onAcknowledge={acknowledgeBadges} />
    </section>
  )
}

/**
 * Plus och minus med stora träffytor.
 *
 * <para>
 * Ska gå att använda med en hand, stående vid en plan eller sittande i en bil. Knapparna
 * är därför 44 px och står isär — ett felklick här är inte farligt, men irriterande nog
 * att någon slutar fylla i.
 * </para>
 *
 * <para>
 * Minusknappen stängs av vid noll i stället för att räkna vidare nedåt. Ett negativt antal
 * mål betyder ingenting, och en spärr som syns är bättre än en som tyst rättar.
 * </para>
 */
function Stepper({
  label,
  shortLabel,
  value,
  onChange,
}: {
  label: string
  shortLabel?: string
  value: number
  onChange: (delta: number) => void
}) {
  return (
    <div className="stepper">
      <span className="stepper__label">{shortLabel ?? label}</span>

      <div className="stepper__controls">
        <button
          type="button"
          className="stepper__button"
          disabled={value === 0}
          onClick={() => {
            onChange(-1)
          }}
        >
          <span aria-hidden="true">−</span>
          <span className="visually-hidden">{`Minska ${label}`}</span>
        </button>

        <output className="stepper__value" aria-label={label}>
          {value}
        </output>

        <button
          type="button"
          className="stepper__button"
          onClick={() => {
            onChange(1)
          }}
        >
          <span aria-hidden="true">+</span>
          <span className="visually-hidden">{`Öka ${label}`}</span>
        </button>
      </div>
    </div>
  )
}

export type { MatchReport }
