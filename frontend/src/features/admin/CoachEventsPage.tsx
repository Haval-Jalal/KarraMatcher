import { useQueryClient } from '@tanstack/react-query'
import { useParams } from '@tanstack/react-router'
import { useState } from 'react'

import { CarpoolOverview } from '@/features/carpool'
import { TeamRosterSection } from '@/features/children'
import { useAuth } from '@/features/auth'
import { homeSummaryQueryKey } from '@/features/home/useHomeSummary'
import {
  eventLabel,
  eventQueryKey,
  teamEventsQueryKey,
  useTeamEvents,
  type TeamEvent,
} from '@/features/events'
import { ApiError } from '@/lib/api'
import { useDocumentTitle } from '@/lib/useDocumentTitle'

import { cancelEvent, createEvent, deleteEvent, updateEvent } from './adminApi'
import { EventForm } from './EventForm'
import { ScheduleImport } from './ScheduleImport'
import { SeasonOverview } from './SeasonOverview'

function messageOf(error: unknown): string {
  if (error instanceof ApiError) {
    return error.offline ? 'Ingen anslutning. Kontrollera nätet och försök igen.' : error.message
  }

  return 'Något gick fel. Försök igen om en stund.'
}

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
  const [failure, setFailure] = useState<string | null>(null)

  useDocumentTitle('Sköt laget')

  // Efter en ändring: lagets schema alltid, och den enskilda händelsen när ändringen rör en
  // känd händelse — annars behåller en öppen händelsesida gammal tid/motståndare/inställt-läge
  // (#393).
  const refresh = async (eventId?: string) => {
    await queryClient.invalidateQueries({ queryKey: teamEventsQueryKey(slug) })
    // Hem-vyn visar nästa händelse — håll den i takt när en händelse skapas/ändras/ställs in (#476).
    await queryClient.invalidateQueries({ queryKey: homeSummaryQueryKey })
    if (eventId !== undefined) {
      await queryClient.invalidateQueries({ queryKey: eventQueryKey(eventId) })
    }
  }

  async function handleCancel(event: TeamEvent): Promise<void> {
    setFailure(null)
    try {
      await cancelEvent(slug, event.id)
      await refresh(event.id)
    } catch (error) {
      setFailure(messageOf(error))
    }
  }

  async function handleDelete(event: TeamEvent): Promise<void> {
    setFailure(null)
    try {
      await deleteEvent(slug, event.id)
      await refresh(event.id)
      setConfirmDelete(null)
    } catch (error) {
      // Panelen står kvar öppen så tränaren ser felet och kan försöka igen eller avbryta.
      setFailure(messageOf(error))
    }
  }

  /*
   * En trupp-tränare känns igen på lagets trupp-id, som kommer med schemat. Därför avgörs
   * behörigheten forst nar datan finns -- annars vore en trupp-tränare nekad medan schemat
   * ännu laddar (`#287`). Servern avgor vad nagon far gora; det har avgor bara vad som visas.
   */
  const mayManage = canManage(slug, data?.truppId)

  if (isPending) {
    return (
      <main>
        <header className="app-header">
          <h1>Sköt laget</h1>
        </header>
        <p className="state" role="status">
          Hämtar schemat…
        </p>
      </main>
    )
  }

  if (!mayManage || !data) {
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
        <p className="app-header__subtitle">Ändringar syns direkt för föräldrarna i schemat.</p>
      </header>

      {adding || editing !== null ? (
        <EventForm
          truppId={data.truppId}
          {...(editing !== null ? { existing: editing } : {})}
          onSubmit={async (input) => {
            // EventForm fångar och visar ett fel som kastas här; vid succé stänger vi formuläret.
            if (editing !== null) {
              await updateEvent(slug, editing.id, input)
              await refresh(editing.id)
            } else {
              await createEvent(slug, input)
              await refresh()
            }

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
        Laget (barn + vårdnadshavares kontakt) — tränarens läsvy av sitt eget lag (`#redesign`).
        Servern scopar svaret till laget; tränaren ser aldrig andra lags barn (§KM.3).
      */}
      <TeamRosterSection slug={slug} />

      {/*
        Samåkningen står före säsongslistan: den är det som är färskvara.
      */}
      <CarpoolOverview slug={slug} enabled={mayManage} />

      <h2 className="match-list__title">Hela säsongen</h2>

      {failure !== null && confirmDelete === null && (
        <p className="state state--error" role="alert">
          {failure}
        </p>
      )}

      <SeasonOverview
        events={data?.events ?? []}
        onEdit={(event) => {
          setFailure(null)
          setEditing(event)
        }}
        onCancel={(event) => {
          void handleCancel(event)
        }}
        onDelete={(event) => {
          setFailure(null)
          setConfirmDelete(event)
        }}
      />

      {confirmDelete !== null && (
        <section className="danger-zone">
          <h2>Ta bort {eventLabel(confirmDelete)}?</h2>

          <p className="state" role="alert">
            Händelsen försvinner helt ur schemat.{' '}
            <strong>Ska en match ställas in ska du välja Ställ in i stället</strong> — då blir den
            kvar i schemat, markerad som inställd.
          </p>

          {failure !== null && (
            <p className="state state--error" role="alert">
              {failure}
            </p>
          )}

          <div className="actions">
            <button
              type="button"
              className="button"
              onClick={() => {
                setFailure(null)
                setConfirmDelete(null)
              }}
            >
              Avbryt
            </button>
            <button
              type="button"
              className="button button--danger"
              onClick={() => {
                void handleDelete(confirmDelete)
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
