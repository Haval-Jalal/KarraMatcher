import { useRef, useState } from 'react'

import { ApiError } from '@/lib/api'

import { useClubVenue, useSetClubVenue } from './useClubVenue'

function messageOf(error: unknown): string {
  if (error instanceof ApiError) {
    return error.offline ? 'Ingen anslutning. Försök igen.' : error.message
  }

  return 'Något gick fel. Försök igen om en stund.'
}

/**
 * Truppens hemmaplan i inställningarna (`#307`/`#405`).
 *
 * <para>
 * En admin för truppen skriver in truppens plan (namn + adress). Servern geokodar adressen så
 * väder och vägbeskrivning får koordinater. Planen hör till truppen — en annan trupp har sin egen —
 * och när en aktivitet läggs upp "hemma" autofylls den här adressen.
 * </para>
 */
export function ClubVenueSettings({ truppId }: { truppId: string }) {
  const venue = useClubVenue(truppId)
  const save = useSetClubVenue(truppId)

  // Fälten härleds i stället för att speglas via en effect (undviker setState-i-effect): null
  // = "orörd", då visas den sparade planen; så fort användaren skriver håller state deras text.
  const [name, setName] = useState<string | null>(null)
  const [address, setAddress] = useState<string | null>(null)
  const [saved, setSaved] = useState(false)
  const [failure, setFailure] = useState<string | null>(null)
  // Vilket fält som flaggats ogiltigt, för aria-invalid + fokus (#615). Nollas när användaren skriver.
  const [invalidField, setInvalidField] = useState<'name' | 'address' | null>(null)

  const nameRef = useRef<HTMLInputElement>(null)
  const addressRef = useRef<HTMLInputElement>(null)

  const nameValue = name ?? venue.data?.name ?? ''
  const addressValue = address ?? venue.data?.address ?? ''

  function submit(event: React.FormEvent): void {
    event.preventDefault()
    setFailure(null)
    setSaved(false)

    // Flagga och fokusera det första tomma fältet i stället för att bara visa en lös felrad —
    // en skärmläsar- eller tangentbords-användare förs då till det som behöver åtgärdas (#615).
    const firstEmpty =
      nameValue.trim() === '' ? 'name' : addressValue.trim() === '' ? 'address' : null

    if (firstEmpty !== null) {
      setInvalidField(firstEmpty)
      setFailure('Fyll i både namn och adress.')
      ;(firstEmpty === 'name' ? nameRef : addressRef).current?.focus()
      return
    }

    setInvalidField(null)

    save.mutate(
      { name: nameValue.trim(), address: addressValue.trim() },
      {
        onSuccess: () => setSaved(true),
        onError: (error) => setFailure(messageOf(error)),
      },
    )
  }

  return (
    <section className="club-venue">
      <h3>Truppens hemmaplan</h3>
      <p className="admin-muted">
        Adressen som fylls i automatiskt när en aktivitet läggs upp <strong>hemma</strong>. Gäller
        den här truppen — en annan trupp har sin egen.
      </p>

      {venue.isPending && (
        <p className="state" role="status">
          Hämtar…
        </p>
      )}

      {venue.isError && (
        <div className="state state--error" role="alert">
          <p>
            {venue.error instanceof ApiError && venue.error.offline
              ? 'Ingen anslutning. Hemmaplanen kan inte hämtas just nu.'
              : 'Kunde inte hämta truppens hemmaplan just nu.'}
          </p>
          <button
            type="button"
            className="button"
            onClick={() => {
              void venue.refetch()
            }}
            disabled={venue.isFetching}
          >
            {venue.isFetching ? 'Försöker…' : 'Försök igen'}
          </button>
        </div>
      )}

      {venue.data && (
        <form className="form" noValidate onSubmit={submit}>
          <div className="form__field">
            <label htmlFor="club-venue-name">Namn på planen</label>
            <input
              ref={nameRef}
              id="club-venue-name"
              type="text"
              autoComplete="off"
              value={nameValue}
              aria-invalid={invalidField === 'name' ? true : undefined}
              aria-describedby={invalidField === 'name' ? 'club-venue-fel' : undefined}
              onChange={(event) => {
                setName(event.target.value)
                setSaved(false)
                setInvalidField(null)
              }}
            />
          </div>

          <div className="form__field">
            <label htmlFor="club-venue-address">Adress</label>
            <input
              ref={addressRef}
              id="club-venue-address"
              type="text"
              autoComplete="off"
              placeholder="t.ex. Klarebergsvallen, Göteborg"
              value={addressValue}
              aria-invalid={invalidField === 'address' ? true : undefined}
              aria-describedby={invalidField === 'address' ? 'club-venue-fel' : undefined}
              onChange={(event) => {
                setAddress(event.target.value)
                setSaved(false)
                setInvalidField(null)
              }}
            />
            <p className="admin-muted">Skriv gatunamn och ort så hittas rätt plats.</p>
          </div>

          {failure !== null && (
            <p id="club-venue-fel" className="state state--error" role="alert">
              {failure}
            </p>
          )}

          {saved && (
            <p className="state" role="status">
              Sparat. Hemma-adressen fylls nu i automatiskt.
            </p>
          )}

          <div className="actions">
            <button type="submit" className="button button--action" disabled={save.isPending}>
              {venue.data.configured ? 'Spara ändring' : 'Spara hemmaplan'}
            </button>
          </div>
        </form>
      )}
    </section>
  )
}
