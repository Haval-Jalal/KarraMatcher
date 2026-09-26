import { useEffect, useState } from 'react'

import { ApiError } from '@/lib/api'

import { useClubVenue, useSetClubVenue } from './useClubVenue'

function messageOf(error: unknown): string {
  if (error instanceof ApiError) {
    return error.offline ? 'Ingen anslutning. Försök igen.' : error.message
  }

  return 'Något gick fel. Försök igen om en stund.'
}

/**
 * Klubbens hemmaplan i inställningarna (`#307`).
 *
 * <para>
 * Vilken tränare/admin som helst i klubben kan skriva in klubbens plan (namn + adress).
 * Servern geokodar adressen så väder och vägbeskrivning får koordinater. Planen delas av alla
 * klubbens truppar, och när en aktivitet läggs upp "hemma" autofylls den här adressen.
 * </para>
 */
export function ClubVenueSettings({ truppId }: { truppId: string }) {
  const venue = useClubVenue(truppId)
  const save = useSetClubVenue(truppId)

  const [name, setName] = useState('')
  const [address, setAddress] = useState('')
  const [saved, setSaved] = useState(false)
  const [failure, setFailure] = useState<string | null>(null)

  // Förfyll fälten med den sparade planen när den lästs in.
  useEffect(() => {
    if (venue.data) {
      setName(venue.data.name ?? '')
      setAddress(venue.data.address ?? '')
    }
  }, [venue.data])

  function submit(event: React.FormEvent): void {
    event.preventDefault()
    setFailure(null)
    setSaved(false)

    if (name.trim() === '' || address.trim() === '') {
      setFailure('Fyll i både namn och adress.')
      return
    }

    save.mutate(
      { name: name.trim(), address: address.trim() },
      {
        onSuccess: () => setSaved(true),
        onError: (error) => setFailure(messageOf(error)),
      },
    )
  }

  return (
    <section className="club-venue">
      <h3>Klubbens hemmaplan</h3>
      <p className="admin-muted">
        Adressen som fylls i automatiskt när en aktivitet läggs upp <strong>hemma</strong>. Delas av
        alla klubbens truppar.
      </p>

      {venue.isPending && (
        <p className="state" role="status">
          Hämtar…
        </p>
      )}

      {venue.isError && (
        <p className="state state--error" role="alert">
          Kunde inte hämta klubbens hemmaplan.
        </p>
      )}

      {venue.data && (
        <form className="form" noValidate onSubmit={submit}>
          <div className="form__field">
            <label htmlFor="club-venue-name">Namn på planen</label>
            <input
              id="club-venue-name"
              type="text"
              autoComplete="off"
              value={name}
              onChange={(event) => {
                setName(event.target.value)
                setSaved(false)
              }}
            />
          </div>

          <div className="form__field">
            <label htmlFor="club-venue-address">Adress</label>
            <input
              id="club-venue-address"
              type="text"
              autoComplete="off"
              placeholder="t.ex. Klarebergsvallen, Göteborg"
              value={address}
              onChange={(event) => {
                setAddress(event.target.value)
                setSaved(false)
              }}
            />
            <p className="admin-muted">Skriv gatunamn och ort så hittas rätt plats.</p>
          </div>

          {failure !== null && (
            <p className="state state--error" role="alert">
              {failure}
            </p>
          )}

          {saved && (
            <p className="state" role="status">
              Sparat. Hemma-adressen fylls nu i automatiskt.
            </p>
          )}

          <div className="actions">
            <button type="submit" className="button" disabled={save.isPending}>
              {venue.data.configured ? 'Spara ändring' : 'Spara hemmaplan'}
            </button>
          </div>
        </form>
      )}
    </section>
  )
}
