import { useQueryClient } from '@tanstack/react-query'
import { useState } from 'react'

import type { EventInput } from '@/features/admin/adminApi'
import { EventForm } from '@/features/admin/EventForm'
import { setKallelse } from '@/features/attendance/attendanceApi'
import { useRoster } from '@/features/children'
import { ApiError } from '@/lib/api'

import { createTruppEvent } from './activitiesApi'
import { TargetGroupPicker } from './TargetGroupPicker'
import { defaultTargetFor, type KallelseTarget, resolveTarget, typeHasTarget } from './target'

/**
 * Admin skapar en aktivitet på trupp-nivå och väljer kallelse-målgrupp i samma steg (§KM.7,
 * `#333`, epic #330).
 *
 * <h3>Ett steg, två anrop</h3>
 *
 * Formuläret gör händelsen (`POST …/events`) och sätter sedan kallelsen (`PUT …/kallelse`) för de
 * barn målgruppen löser sig till. Bara match och träning har en vanlig kallelse här — cup får sin
 * platstak-kallelse i ett senare steg (`#335`), övrigt kallar aldrig — så för dem skapas bara
 * händelsen.
 *
 * <h3>Målgruppen styr både lag-märket och vilka som kallas</h3>
 *
 * Se {@link resolveTarget}: ett enda valt lag märker händelsen (så en match syns i det lagets
 * schema), medan hela truppen / flera lag / namngivna barn ger en trupp-vid händelse.
 */
export function CreateActivity({ truppId }: { truppId: string }) {
  const roster = useRoster(truppId)
  const queryClient = useQueryClient()

  const [type, setType] = useState<EventInput['type']>('Match')
  const [target, setTarget] = useState<KallelseTarget>(() => defaultTargetFor('Match'))
  const [done, setDone] = useState<{ called: number } | null>(null)
  const [failure, setFailure] = useState<string | null>(null)

  async function handleSubmit(input: EventInput): Promise<void> {
    setFailure(null)

    const rosterData = roster.data
    const { eventTeamId, childIds } =
      typeHasTarget(input.type) && rosterData
        ? resolveTarget(target, rosterData)
        : { eventTeamId: null, childIds: [] }

    // Händelsen först — dess id behövs för att sätta kallelsen. Ett fel här kastar vidare till
    // EventForm, som visar det; ingen kallelse skickas då.
    const created = await createTruppEvent(truppId, { ...input, teamId: eventTeamId })

    if (typeHasTarget(input.type) && childIds.length > 0) {
      // Händelsen finns nu även om kallelsen skulle strula — säg det ärligt så admin kan sätta
      // kallelsen från händelsesidan i stället för att tro att inget skapades.
      try {
        await setKallelse(truppId, created.id, childIds)
      } catch (error) {
        setFailure(
          error instanceof ApiError && !error.offline
            ? `Aktiviteten skapades, men kallelsen gick inte att skicka: ${error.message} Sätt den från händelsen i stället.`
            : 'Aktiviteten skapades, men kallelsen gick inte att skicka. Kontrollera nätet och sätt den från händelsen.',
        )

        await queryClient.invalidateQueries()

        return
      }
    }

    await queryClient.invalidateQueries()
    setDone({ called: typeHasTarget(input.type) ? childIds.length : 0 })
  }

  function reset(): void {
    setDone(null)
    setFailure(null)
    setType('Match')
    setTarget(defaultTargetFor('Match'))
  }

  if (done !== null) {
    return (
      <section className="admin-panel__section">
        <p className="state" role="status">
          Aktiviteten är skapad.{' '}
          {done.called > 0
            ? `Kallelsen gick till ${done.called} barn.`
            : 'Ingen kallelse skickades.'}
        </p>
        <div className="actions">
          <button type="button" className="button" onClick={reset}>
            Skapa en till
          </button>
        </div>
      </section>
    )
  }

  return (
    <section className="admin-panel__section">
      <h2>Skapa aktivitet</h2>
      <p className="admin-muted">
        Välj typ och vilka som ska kallas. Match och träning skickar en kallelse direkt; cup och
        övrigt lägger bara upp aktiviteten.
      </p>

      {roster.isError && (
        <p className="state state--error" role="alert">
          {roster.error instanceof ApiError && roster.error.offline
            ? 'Ingen anslutning. Kontrollera nätet och försök igen.'
            : 'Kunde inte hämta truppens barn och lag. Ladda om och försök igen.'}
        </p>
      )}

      <EventForm
        truppId={truppId}
        onTypeChange={(next) => {
          setType(next)
          setTarget(defaultTargetFor(next))
        }}
        targetSlot={
          typeHasTarget(type) ? (
            roster.isPending ? (
              <p className="state" role="status">
                Hämtar truppens barn och lag…
              </p>
            ) : roster.data ? (
              <TargetGroupPicker roster={roster.data} value={target} onChange={setTarget} />
            ) : null
          ) : (
            <p className="admin-muted">
              {type === 'Cup'
                ? 'Cup-kallelse med platstak kommer i ett senare steg.'
                : 'Övriga händelser har ingen kallelse.'}
            </p>
          )
        }
        onSubmit={handleSubmit}
        onCancel={reset}
      />

      {failure !== null && (
        <p className="state state--error" role="alert">
          {failure}
        </p>
      )}
    </section>
  )
}
