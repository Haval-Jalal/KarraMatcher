import { useQueryClient } from '@tanstack/react-query'
import { useState } from 'react'

import type { TeamEvent } from '@/features/events'
import { ApiError } from '@/lib/api'

import { createOffer } from './carpoolApi'
import { CarpoolOfferCard } from './CarpoolOfferCard'
import { CarpoolOfferForm } from './CarpoolOfferForm'
import { carpoolOffersQueryKey, useCarpoolOffers } from './useCarpool'

/**
 * Samåkningen för en match (`#53`, §KM.12).
 *
 * <h3>Varför den ligger på matchsidan</h3>
 *
 * Samåkning hör ihop med en enda match — vem som kör till lördagens bortamatch säger
 * ingenting om nästa. Att lägga den här betyder också att den hittas av den som redan
 * tittar på tiden och platsen, vilket är när frågan uppstår.
 *
 * <h3>Måttet den ska mätas mot</h3>
 *
 * Att det ska vara enklare än att skriva i föräldrachatten. Är det inte det används den
 * inte, och då spelar det ingen roll vad den kan.
 */
export function CarpoolSection({ match }: { match: TeamEvent }) {
  const queryClient = useQueryClient()
  const [adding, setAdding] = useState(false)

  const { data: offers, isPending, error, refetch, isFetching } = useCarpoolOffers(match.id)

  async function reload(): Promise<void> {
    await queryClient.invalidateQueries({ queryKey: carpoolOffersQueryKey(match.id) })
  }

  return (
    <section className="carpool" aria-labelledby="samakning">
      <h2 id="samakning" className="carpool__heading">
        Samåkning
      </h2>

      {isPending && <p className="carpool__empty">Hämtar samåkningen…</p>}

      {error !== null && !isPending && (
        <div className="state state--error" role="alert">
          <p>{errorMessage(error)}</p>
          <button
            type="button"
            className="button"
            disabled={isFetching}
            onClick={() => {
              void refetch()
            }}
          >
            {isFetching ? 'Försöker…' : 'Försök igen'}
          </button>
        </div>
      )}

      {offers !== undefined && offers.length === 0 && (
        <p className="carpool__empty">Ingen har erbjudit skjuts till den här matchen än.</p>
      )}

      {offers !== undefined && offers.length > 0 && (
        <ul className="carpool__offers">
          {offers.map((offer) => (
            <CarpoolOfferCard key={offer.id} matchId={match.id} offer={offer} onChanged={reload} />
          ))}
        </ul>
      )}

      {/*
        Ingen gäst-variant här längre: hela händelsesidan kräver inloggning (§KM.3, `#242`),
        så den som ser samåkningen är alltid medlem. En utloggad möts av inloggningen redan
        på vägen in, inte av en knapp som svarar `401`.
      */}
      {adding ? (
        <CarpoolOfferForm
          kickoffUtc={match.kickoffUtc}
          onSubmit={async (input) => {
            await createOffer(match.id, input)
            setAdding(false)
            await reload()
          }}
          onCancel={() => {
            setAdding(false)
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
            Erbjud skjuts
          </button>
        </div>
      )}
    </section>
  )
}

function errorMessage(error: unknown): string {
  return error instanceof ApiError && error.offline
    ? 'Ingen anslutning. Samåkningen kan inte hämtas just nu — resten av matchen står kvar.'
    : 'Kunde inte hämta samåkningen just nu.'
}
