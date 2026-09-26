import { Link, useParams } from '@tanstack/react-router'
import { useState } from 'react'

import { useMyTrupper } from '@/features/chat'
import { formatKickoffTime, formatMatchDate } from '@/lib/time'
import { useDocumentTitle } from '@/lib/useDocumentTitle'

import type { CupListItem } from './cupApi'
import { useTruppCups } from './useCup'

/** Kort statusrad för en cup: platser kvar, fullt, eller att anmälan inte öppnat. */
function stateText(cup: CupListItem): string {
  if (!cup.open) {
    return 'Anmälan inte öppen än'
  }

  if (cup.isFull) {
    return 'Fullt'
  }

  return `${cup.spotsLeft} av ${cup.capacity} platser kvar`
}

/**
 * Truppens cuper på ett ställe (`#304`). En cup drar barn tvärs över färg-lagen, så listan är
 * trupp-vid (till skillnad från matcher/träningar som hör till ett lag). Varje cup länkar till
 * sin händelsesida, där man anmäler sitt barn (`#296`).
 */
export function CuperPage() {
  const params = useParams({ strict: false })
  const routeTruppId = typeof params.truppId === 'string' ? params.truppId : null

  const trupper = useMyTrupper()
  const [chosen, setChosen] = useState<string | null>(null)

  const options = trupper.data ?? []
  const truppId = chosen ?? routeTruppId ?? options[0]?.id ?? null
  const cups = useTruppCups(truppId)

  useDocumentTitle('Cuper')

  return (
    <main className="page">
      <header className="app-header">
        <h1>Cuper</h1>
      </header>

      {trupper.isLoading && <p className="state">Hämtar…</p>}
      {trupper.data && options.length === 0 && (
        <p className="state">Du är inte medlem i någon trupp än.</p>
      )}

      {options.length > 1 && (
        <div className="form__field">
          <label htmlFor="cuper-valj-trupp">Välj trupp</label>
          <select
            id="cuper-valj-trupp"
            value={truppId ?? ''}
            onChange={(event) => setChosen(event.target.value)}
          >
            {options.map((trupp) => (
              <option key={trupp.id} value={trupp.id}>
                {trupp.clubName} · {trupp.name} {trupp.season}
              </option>
            ))}
          </select>
        </div>
      )}

      {truppId !== null && (
        <>
          {cups.isPending && (
            <p className="state" role="status">
              Hämtar cuper…
            </p>
          )}

          {cups.isError && (
            <p className="state state--error" role="alert">
              Kunde inte hämta cuperna.
            </p>
          )}

          {cups.data && cups.data.length === 0 && <p className="state">Inga cuper än.</p>}

          {cups.data && cups.data.length > 0 && (
            <ul className="cup-list">
              {cups.data.map((cup) => (
                <li key={cup.eventId}>
                  <Link className="cup-card" to="/handelse/$id" params={{ id: cup.eventId }}>
                    <span className="cup-card__title">{cup.title}</span>
                    <span className="cup-card__meta">
                      {formatMatchDate(cup.kickoffUtc)} {formatKickoffTime(cup.kickoffUtc)} ·{' '}
                      {cup.teamName}
                    </span>
                    <span
                      className={
                        cup.isFull ? 'cup-card__state cup-card__state--full' : 'cup-card__state'
                      }
                    >
                      {stateText(cup)}
                    </span>
                  </Link>
                </li>
              ))}
            </ul>
          )}
        </>
      )}
    </main>
  )
}
