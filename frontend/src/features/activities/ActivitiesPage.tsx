import { useParams } from '@tanstack/react-router'
import { useState } from 'react'

import { useAuth } from '@/features/auth'
import { useMyTrupper } from '@/features/chat'
import { EventList } from '@/features/events'
import { ApiError } from '@/lib/api'
import { useDocumentTitle } from '@/lib/useDocumentTitle'

import { CreateActivity } from './CreateActivity'
import { ManageActivities } from './ManageActivities'
import { useTruppActivities } from './useActivities'

/**
 * Aktivitet-fliken (`#334`, epic #330): truppens alla aktiviteter på ett ställe.
 *
 * <h3>Samma lista, två roller</h3>
 *
 * Alla medlemmar ser samma **läslista** — matcher, träningar, cuper och övrigt, tvärs över
 * färg-lagen — och trycker in på en aktivitet för dagens info och samåkning (som idag). En
 * <b>admin</b> för truppen ser dessutom en <em>Skapa aktivitet</em>-ingång (slice 2-flödet).
 * Knappen är bara bekvämlighet: servern grindar skapandet (<c>AdminOfTrupp</c>), och läslistan
 * bakom medlemskap (<c>MemberOfTrupp</c>, §KM.3).
 *
 * <h3>Trupp-väljaren</h3>
 *
 * Förvald när man bara är med i en trupp; väljaren visas först för den som är med i flera. Samma
 * mönster som Cuper-fliken.
 */
export function ActivitiesPage() {
  const { isSuperAdmin, adminOf } = useAuth()
  const params = useParams({ strict: false })
  const routeTruppId = typeof params.truppId === 'string' ? params.truppId : null

  const trupper = useMyTrupper()
  const [chosen, setChosen] = useState<string | null>(null)
  const [creating, setCreating] = useState(false)

  const options = trupper.data ?? []
  const truppId = chosen ?? routeTruppId ?? options[0]?.id ?? null
  const activities = useTruppActivities(truppId)

  useDocumentTitle('Aktivitet')

  const canCreate = truppId !== null && (isSuperAdmin || adminOf.includes(truppId))

  return (
    <main className="page">
      <header className="app-header">
        <h1>Aktivitet</h1>
        <p className="app-header__subtitle">Truppens matcher, träningar och cuper.</p>
      </header>

      {trupper.isLoading && (
        <p className="state" role="status">
          Hämtar…
        </p>
      )}
      {trupper.isError && (
        <p className="state state--error" role="alert">
          {trupper.error instanceof ApiError && trupper.error.offline
            ? 'Ingen anslutning. Kontrollera nätet och försök igen.'
            : 'Kunde inte hämta dina trupper just nu.'}
        </p>
      )}
      {trupper.data && options.length === 0 && (
        <p className="state">Du är inte medlem i någon trupp än.</p>
      )}

      {options.length > 1 && (
        <div className="form__field">
          <label htmlFor="aktivitet-valj-trupp">Välj trupp</label>
          <select
            id="aktivitet-valj-trupp"
            value={truppId ?? ''}
            onChange={(event) => {
              setChosen(event.target.value)
              setCreating(false)
            }}
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
          {canCreate && (
            <button
              type="button"
              className="add-fab"
              aria-expanded={creating}
              onClick={() => setCreating((open) => !open)}
            >
              {creating ? (
                'Stäng'
              ) : (
                <>
                  <span className="add-fab__plus" aria-hidden="true">
                    ＋
                  </span>{' '}
                  Skapa aktivitet
                </>
              )}
            </button>
          )}

          {creating && canCreate && <CreateActivity truppId={truppId} />}

          {activities.isPending && (
            <p className="state" role="status">
              Hämtar aktiviteter…
            </p>
          )}

          {activities.isError && (
            <p className="state state--error" role="alert">
              {activities.error instanceof ApiError && activities.error.offline
                ? 'Ingen anslutning. Kontrollera nätet och försök igen.'
                : 'Kunde inte hämta aktiviteterna.'}
            </p>
          )}

          {activities.data &&
            (activities.data.length === 0 ? (
              <p className="state">Inga aktiviteter är inlagda än.</p>
            ) : canCreate ? (
              // Admin: hanterbar tabell (ändra/ställ in/ta bort) i stället för läslistan (#408).
              <ManageActivities truppId={truppId} activities={activities.data} />
            ) : (
              <EventList events={activities.data.map((item) => item.event)} />
            ))}
        </>
      )}
    </main>
  )
}
