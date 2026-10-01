import { useQueryClient } from '@tanstack/react-query'
import { useEffect, useRef, useState } from 'react'

import type { EventInput } from '@/features/admin/adminApi'
import { EventForm, SeasonOverview } from '@/features/admin'
import { cupKeys } from '@/features/cup/useCup'
import { eventLabel, eventQueryKey, type TeamEvent } from '@/features/events'
import { homeSummaryQueryKey } from '@/features/home/useHomeSummary'
import { ApiError } from '@/lib/api'

import {
  cancelTruppEvent,
  deleteTruppEvent,
  updateTruppEvent,
  type Activity,
} from './activitiesApi'
import { activitiesKeys } from './useActivities'

function messageOf(error: unknown): string {
  if (error instanceof ApiError) {
    return error.offline ? 'Ingen anslutning. Kontrollera nätet och försök igen.' : error.message
  }

  return 'Något gick fel. Försök igen om en stund.'
}

/**
 * Adminens hantering av truppens aktiviteter (`#408`): ändra, ställ in eller ta bort — för både
 * trupp-övergripande och lag-riktade händelser i truppen. Backend fanns redan
 * (`TruppEventAdminController`); det här är gränssnittet som saknades.
 *
 * <para>
 * Samma tabell och formulär som tränarens lag-vy (<c>SeasonOverview</c>/<c>EventForm</c>), men
 * riktat mot truppens admin-endpoints. Behörigheten prövas server-side (<c>AdminOfTrupp</c>);
 * knapparna här speglar bara vad som går att göra.
 * </para>
 */
export function ManageActivities({
  truppId,
  activities,
}: {
  truppId: string
  activities: Activity[]
}) {
  const queryClient = useQueryClient()
  const [editing, setEditing] = useState<TeamEvent | null>(null)
  const [confirmDelete, setConfirmDelete] = useState<TeamEvent | null>(null)
  const [failure, setFailure] = useState<string | null>(null)
  // Kvitto efter en lyckad ställ-in/ta-bort. Nonce så samma text annonseras (och tar fokus) igen
  // vid upprepade åtgärder (#544, WCAG 4.1.3).
  const [notice, setNotice] = useState<{ text: string; seq: number } | null>(null)
  const seqRef = useRef(0)
  const statusRef = useRef<HTMLParagraphElement>(null)
  const confirmHeadingRef = useRef<HTMLHeadingElement>(null)
  const listRef = useRef<HTMLDivElement>(null)

  function announce(text: string): void {
    seqRef.current += 1
    setNotice({ text, seq: seqRef.current })
  }

  // Flytta fokus till kvittot när det annonseras. Vid en borttagning unmontas danger-zonen med den
  // fokuserade "Ja, ta bort"-knappen, så utan detta faller fokus till <body> (WCAG 2.4.3).
  useEffect(() => {
    if (notice !== null) {
      statusRef.current?.focus()
    }
  }, [notice])

  // Flytta fokus till bekräftelsens rubrik när den öppnas (WCAG 2.4.3).
  useEffect(() => {
    if (confirmDelete !== null) {
      confirmHeadingRef.current?.focus()
    }
  }, [confirmDelete])

  const events = activities.map((item) => item.event)

  // Efter en ändring: truppens aktivitetslista alltid, och den enskilda händelsen när ändringen
  // rör en känd händelse — annars behåller en öppen händelsesida gammalt läge (samma som #393).
  async function refresh(eventId?: string): Promise<void> {
    await queryClient.invalidateQueries({ queryKey: activitiesKeys.list(truppId) })
    // Hem-vyn visar nästa händelse — håll den i takt när en händelse skapas/ändras/ställs in (#476).
    await queryClient.invalidateQueries({ queryKey: homeSummaryQueryKey })
    // En lag-riktad händelse som ändras/ställs in/tas bort här syns också i tränarens och
    // föräldrarnas lag-schema (['team-events',slug]) och, för en cup, i trupp-cuplistan. Utan
    // detta står den kvar inaktuell där tills omladdning (#537). Slugen är okänd här → prefixet.
    await queryClient.invalidateQueries({ queryKey: ['team-events'] })
    await queryClient.invalidateQueries({ queryKey: cupKeys.truppCups(truppId) })
    if (eventId !== undefined) {
      await queryClient.invalidateQueries({ queryKey: eventQueryKey(eventId) })
    }
  }

  async function handleCancel(event: TeamEvent): Promise<void> {
    setFailure(null)
    try {
      await cancelTruppEvent(truppId, event.id)
      await refresh(event.id)
      announce('Aktiviteten ställdes in.')
    } catch (error) {
      setFailure(messageOf(error))
    }
  }

  async function handleDelete(event: TeamEvent): Promise<void> {
    setFailure(null)
    try {
      await deleteTruppEvent(truppId, event.id)
      await refresh(event.id)
      setConfirmDelete(null)
      announce('Aktiviteten togs bort.')
    } catch (error) {
      setFailure(messageOf(error))
    }
  }

  if (editing !== null) {
    return (
      <EventForm
        truppId={truppId}
        existing={editing}
        onSubmit={async (input: EventInput) => {
          // EventForm fångar och visar ett fel som kastas här; vid succé stänger vi formuläret.
          await updateTruppEvent(truppId, editing.id, input)
          await refresh(editing.id)
          setEditing(null)
        }}
        onCancel={() => setEditing(null)}
      />
    )
  }

  return (
    <>
      {failure !== null && confirmDelete === null && (
        <p className="state state--error" role="alert">
          {failure}
        </p>
      )}

      {notice !== null && (
        <p className="state state--ok" role="status" tabIndex={-1} ref={statusRef}>
          {notice.text}
        </p>
      )}

      {/* En stabil, fokuserbar region att landa fokus på när bekräftelsen avbryts — annars faller
          fokus till <body> när Avbryt-knappen unmontas (WCAG 2.4.3, #544). */}
      <div ref={listRef} tabIndex={-1}>
        <SeasonOverview
          events={events}
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
      </div>

      {confirmDelete !== null && (
        <section className="danger-zone">
          <h3 tabIndex={-1} ref={confirmHeadingRef}>
            Ta bort {eventLabel(confirmDelete)}?
          </h3>

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
                // Danger-zonen (med denna knapp) unmontas — flytta fokus till schemalistan så det
                // inte faller till <body> (WCAG 2.4.3, #544).
                listRef.current?.focus()
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
    </>
  )
}
