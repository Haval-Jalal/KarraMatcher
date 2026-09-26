import { useState } from 'react'

import { LoadingState } from '@/components/LoadingState'
import { ApiError } from '@/lib/api'

import { useCalendarLink, useRegenerateCalendarLink } from './useCalendar'

/**
 * Kalender-sektionen på Mitt konto (§KM.4, kalender bakom medlemskap).
 *
 * <h3>Matchtiderna i din egen kalender</h3>
 *
 * <para>
 * En personlig länk som telefonens kalender kan prenumerera på — matcherna dyker upp av sig
 * själva och uppdateras när något ändras, en påminnelse appen slipper skicka. Länken är privat
 * och kan bytas ut om den kommit på villovägar; feeden bär bara händelser, aldrig barn-PII.
 * </para>
 */
export function CalendarSection() {
  const link = useCalendarLink()
  const regenerate = useRegenerateCalendarLink()

  const [copied, setCopied] = useState(false)
  const [confirming, setConfirming] = useState(false)

  async function copy(url: string): Promise<void> {
    try {
      await navigator.clipboard.writeText(url)
      setCopied(true)
      window.setTimeout(() => {
        setCopied(false)
      }, 2000)
    } catch {
      // Kunde inte kopiera automatiskt — länken står i fältet att markera för hand.
    }
  }

  return (
    <section aria-labelledby="kalender">
      <h2 className="match-list__title" id="kalender">
        Kalender
      </h2>

      <p className="state">
        Lägg matchtiderna direkt i din telefons kalender — prenumerera på din personliga länk. Den
        uppdateras av sig själv när något ändras.
      </p>

      {link.isPending && <LoadingState label="Hämtar din kalender-länk…" />}

      {link.isError && (
        <div className="state state--error" role="alert">
          <p>
            {link.error instanceof ApiError && link.error.offline
              ? 'Ingen anslutning. Kontrollera nätet och försök igen.'
              : 'Kunde inte hämta kalender-länken just nu.'}
          </p>
          <button
            type="button"
            className="button"
            onClick={() => {
              void link.refetch()
            }}
            disabled={link.isFetching}
          >
            {link.isFetching ? 'Försöker…' : 'Försök igen'}
          </button>
        </div>
      )}

      {link.data && (
        <>
          <div className="actions">
            <input
              type="text"
              className="calendar-link"
              readOnly
              value={link.data.url}
              aria-label="Din kalender-länk"
              onFocus={(event) => {
                event.currentTarget.select()
              }}
            />
            <button
              type="button"
              className="button button--action"
              onClick={() => {
                void copy(link.data.url)
              }}
            >
              {copied ? 'Kopierad' : 'Kopiera'}
            </button>
          </div>

          <p className="admin-muted">
            I Apple Kalender eller Google Kalender: välj <em>Prenumerera på kalender</em> och
            klistra in länken.
          </p>

          {!confirming ? (
            <button
              type="button"
              className="button button--action"
              onClick={() => {
                setConfirming(true)
              }}
            >
              Skapa ny länk…
            </button>
          ) : (
            <div className="state" role="alert">
              <p>
                En ny länk gör att den <strong>gamla slutar fungera</strong>. Du behöver då
                prenumerera på nytt i din kalender.
              </p>
              <div className="actions">
                <button
                  type="button"
                  className="button"
                  onClick={() => {
                    setConfirming(false)
                  }}
                >
                  Avbryt
                </button>
                <button
                  type="button"
                  className="button button--danger"
                  disabled={regenerate.isPending}
                  onClick={() => {
                    regenerate.mutate(undefined, {
                      onSettled: () => {
                        setConfirming(false)
                      },
                    })
                  }}
                >
                  {regenerate.isPending ? 'Skapar…' : 'Ja, skapa ny länk'}
                </button>
              </div>
            </div>
          )}

          {regenerate.isError && (
            <p className="state state--error" role="alert">
              Det gick inte att skapa en ny länk just nu. Försök igen om en stund.
            </p>
          )}
        </>
      )}
    </section>
  )
}
