import { useState } from 'react'

import { useAuth } from '@/features/auth'
import { ApiError } from '@/lib/api'

import { useCupSummary, useOpenCup, useSignUpChild, useWithdrawChild } from './useCup'

function messageOf(error: unknown): string {
  if (error instanceof ApiError) {
    // Serverns svenska felmeddelande (t.ex. "Cupen är fullbokad" vid 409) är redan begripligt.
    return error.offline ? 'Ingen anslutning. Försök igen.' : error.message
  }

  return 'Något gick fel. Försök igen om en stund.'
}

/**
 * Cupens öppna anmälan på händelsesidan (`#295`/`#296`, §KM.12-stil).
 *
 * <para>
 * Tränaren (trupp-bred, prövas server-side) öppnar anmälan och sätter ett platstak. Vilken
 * vårdnadshavare som helst i truppen anmäler sina egna barn — först till kvarn tills platserna
 * är slut. "Fullt" och behörigheten avgörs av servern; knapparna här speglar bara läget.
 * Barn visas som "Liam J" (§KM.1), aldrig hela efternamnet.
 * </para>
 */
export function CupSignupSection({
  eventId,
  truppId,
  teamSlug,
}: {
  eventId: string
  truppId: string
  teamSlug: string
}) {
  const { canManage } = useAuth()
  const isTrainer = canManage(teamSlug, truppId)

  const summary = useCupSummary(eventId)
  const open = useOpenCup(truppId, eventId)
  const signUp = useSignUpChild(eventId)
  const withdraw = useWithdrawChild(eventId)

  const [capacity, setCapacity] = useState('')
  const [failure, setFailure] = useState<string | null>(null)

  const run = (action: Promise<unknown>) => {
    setFailure(null)
    void action.catch((error: unknown) => setFailure(messageOf(error)))
  }

  return (
    <section className="cup" aria-labelledby="cup-heading">
      <h2 id="cup-heading">Cup-anmälan</h2>

      {summary.isPending && (
        <p className="state" role="status">
          Hämtar anmälningsläget…
        </p>
      )}

      {summary.isError && (
        <p className="state state--error" role="alert">
          Kunde inte hämta anmälningsläget.
        </p>
      )}

      {summary.data && (
        <>
          {/* Läget som text — form och ord, aldrig färg ensam (WCAG 1.4.1). */}
          {!summary.data.open ? (
            <p className="state" role="status">
              Anmälan är inte öppen än.
            </p>
          ) : summary.data.isFull ? (
            <p className="notice" role="status">
              <strong>Fullt.</strong> Alla {summary.data.capacity} platser är tagna.
            </p>
          ) : (
            <p className="state" role="status">
              {summary.data.spotsLeft} av {summary.data.capacity} platser kvar.
            </p>
          )}

          {/* Tränaren öppnar eller ändrar platstaket. */}
          {isTrainer && (
            <form
              className="cup__open"
              onSubmit={(event) => {
                event.preventDefault()
                const value = Number(capacity)
                if (!Number.isInteger(value) || value < 1) {
                  setFailure('Ange minst en plats.')
                  return
                }
                run(open.mutateAsync(value).then(() => setCapacity('')))
              }}
            >
              <label htmlFor="cup-capacity">Antal platser</label>
              <input
                id="cup-capacity"
                type="number"
                min={1}
                max={100}
                inputMode="numeric"
                value={capacity}
                onChange={(event) => setCapacity(event.target.value)}
                placeholder={summary.data.capacity?.toString() ?? ''}
              />
              <button type="submit" className="button" disabled={open.isPending}>
                {summary.data.open ? 'Ändra platser' : 'Öppna anmälan'}
              </button>
            </form>
          )}

          {/* Vårdnadshavarens egna barn: anmäl/avanmäl. Bara när anmälan är öppen. */}
          {summary.data.open && summary.data.mine.length > 0 && (
            <ul className="cup__mine">
              {summary.data.mine.map((child) => (
                <li key={child.childId} className="cup__mine-row">
                  <span>{child.displayName}</span>
                  {child.signedUp ? (
                    <button
                      type="button"
                      className="button button--small"
                      disabled={withdraw.isPending}
                      onClick={() => run(withdraw.mutateAsync(child.childId))}
                    >
                      Avanmäl
                    </button>
                  ) : (
                    <button
                      type="button"
                      className="button button--small"
                      disabled={summary.data.isFull || signUp.isPending}
                      onClick={() => run(signUp.mutateAsync(child.childId))}
                    >
                      Anmäl
                    </button>
                  )}
                </li>
              ))}
            </ul>
          )}

          {/* De anmälda barnen — för truppens medlemmar. */}
          {summary.data.signedUp.length > 0 && (
            <div className="cup__list">
              <h3 className="cup__list-title">Anmälda ({summary.data.spotsTaken})</h3>
              <ul>
                {summary.data.signedUp.map((child) => (
                  <li key={child.childId}>{child.displayName}</li>
                ))}
              </ul>
            </div>
          )}

          {failure !== null && (
            <p className="state state--error" role="alert">
              {failure}
            </p>
          )}
        </>
      )}
    </section>
  )
}
