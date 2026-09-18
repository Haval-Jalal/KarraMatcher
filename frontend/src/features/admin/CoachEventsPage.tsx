import { useQueryClient } from '@tanstack/react-query'
import { useParams } from '@tanstack/react-router'
import { useState } from 'react'

import { CarpoolOverview } from '@/features/carpool'
import { useAuth } from '@/features/auth'
import { eventLabel, teamEventsQueryKey, useTeamEvents, type TeamEvent } from '@/features/events'
import { useDocumentTitle } from '@/lib/useDocumentTitle'

import { cancelEvent, createEvent, deleteEvent, updateEvent } from './adminApi'
import { EventForm } from './EventForm'
import { ScheduleImport } from './ScheduleImport'
import { SeasonOverview } from './SeasonOverview'

/**
 * Tränarens vy för ett lag.
 *
 * <h3>Vad den ska kännas som</h3>
 *
 * Det här görs stående vid en plan, på en telefon, ofta med ett barn i handen. Krångel här
 * är den enskilt största risken för att appen aldrig fylls med innehåll.
 *
 * <h3>Ändringen syns direkt</h3>
 *
 * Efter varje ändring ogiltigförklaras lagets schema-fråga, så listan visar det nya utan
 * omladdning.
 */
export function CoachEventsPage() {
  const { slug } = useParams({ from: '/lag/$slug/tranare' })
  const { canManage } = useAuth()
  const queryClient = useQueryClient()
  const { data, isPending } = useTeamEvents(slug)

  const [editing, setEditing] = useState<TeamEvent | null>(null)
  const [adding, setAdding] = useState(false)
  const [confirmDelete, setConfirmDelete] = useState<TeamEvent | null>(null)

  useDocumentTitle('Sköt laget')

  const refresh = async () => {
    await queryClient.invalidateQueries({ queryKey: teamEventsQueryKey(slug) })
  }

  if (!canManage(slug)) {
    /*
     * Servern avgor vad nagon far gora -- det har avgor bara vad som visas.
     */
    return (
      <main>
        <header className="app-header">
          <h1>Sköt laget</h1>
        </header>
        <p className="state state--error" role="alert">
          Du sköter inte det här laget. Kontakta klubben om det borde vara tvärtom.
        </p>
      </main>
    )
  }

  return (
    <main>
      <header className="app-header">
        <h1>Sköt laget</h1>
        <p className="app-header__subtitle">
          Ändringar syns direkt för föräldrarna, och kalenderprenumerationerna uppdateras.
        </p>
      </header>

      {adding || editing !== null ? (
        <EventForm
          {...(editing !== null ? { existing: editing } : {})}
          onSubmit={async (input) => {
            if (editing !== null) {
              await updateEvent(slug, editing.id, input)
            } else {
              await createEvent(slug, input)
            }

            await refresh()
            setAdding(false)
            setEditing(null)
          }}
          onCancel={() => {
            setAdding(false)
            setEditing(null)
          }}
        />
      ) : (
        <div className="actions">
          <button
            type="button"
            className="button"
            onClick={() => {
              setAdding(true)
            }}
          >
            Lägg till händelse
          </button>
        </div>
      )}

      <ScheduleImport
        slug={slug}
        onImported={() => {
          void refresh()
        }}
      />

      {/*
        Samåkningen står före säsongslistan: den är det som är färskvara.
      */}
      <CarpoolOverview slug={slug} enabled={canManage(slug)} />

      <h2 className="match-list__title">Hela säsongen</h2>

      {isPending ? (
        <p className="state" role="status">
          Hämtar schemat…
        </p>
      ) : (
        <SeasonOverview
          events={data?.events ?? []}
          onEdit={setEditing}
          onCancel={(event) => {
            void (async () => {
              await cancelEvent(slug, event.id)
              await refresh()
            })()
          }}
          onDelete={setConfirmDelete}
        />
      )}

      {confirmDelete !== null && (
        <section className="danger-zone">
          <h2>Ta bort {eventLabel(confirmDelete)}?</h2>

          <p className="state" role="alert">
            Händelsen försvinner helt, även ur föräldrarnas kalendrar.{' '}
            <strong>Ska en match ställas in ska du välja Ställ in i stället</strong> — då blir den
            kvar i kalendern, markerad som inställd.
          </p>

          <div className="actions">
            <button
              type="button"
              className="button"
              onClick={() => {
                setConfirmDelete(null)
              }}
            >
              Avbryt
            </button>
            <button
              type="button"
              className="button button--danger"
              onClick={() => {
                void (async () => {
                  await deleteEvent(slug, confirmDelete.id)
                  await refresh()
                  setConfirmDelete(null)
                })()
              }}
            >
              Ja, ta bort
            </button>
          </div>
        </section>
      )}
    </main>
  )
}
