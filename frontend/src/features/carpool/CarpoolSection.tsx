import { useQueryClient } from '@tanstack/react-query'
import { useState } from 'react'

import type { TeamEvent } from '@/features/events'
import { ApiError } from '@/lib/api'

import { createOffer, createRideRequest } from './carpoolApi'
import { CarpoolOfferCard } from './CarpoolOfferCard'
import { CarpoolOfferForm } from './CarpoolOfferForm'
import { CarpoolRideRequestCard } from './CarpoolRideRequestCard'
import { CarpoolRideRequestForm } from './CarpoolRideRequestForm'
import { useCarpoolOffers, useCarpoolRideRequests } from './useCarpool'

/**
 * Samåkningen för en match (`#53`, §KM.12).
 *
 * <h3>Varför den ligger på matchsidan</h3>
 *
 * Samåkning hör ihop med en enda match — vem som kör till lördagens bortamatch säger
 * ingenting om nästa. Att lägga den här betyder också att den hittas av den som redan
 * tittar på tiden och platsen, vilket är när frågan uppstår.
 *
 * <h3>Två håll</h3>
 *
 * En förälder kan antingen <em>erbjuda</em> skjuts eller <em>fråga efter</em> den (`#63`). De två
 * är spegelbilder: i det ena räcker en förare upp handen och föräldrar frågar om plats, i det andra
 * ber en förälder om skjuts och förare erbjuder plats. Båda bor här så att den som saknar skjuts
 * inte behöver vänta på att någon annan lägger upp ett erbjudande.
 *
 * <h3>Måttet den ska mätas mot</h3>
 *
 * Att det ska vara enklare än att skriva i föräldrachatten. Är det inte det används den
 * inte, och då spelar det ingen roll vad den kan.
 */
export function CarpoolSection({ match }: { match: TeamEvent }) {
  const queryClient = useQueryClient()
  const [adding, setAdding] = useState(false)
  const [asking, setAsking] = useState(false)

  const {
    data: offers,
    isPending: offersPending,
    error: offersError,
    refetch: refetchOffers,
    isFetching: offersFetching,
  } = useCarpoolOffers(match.id)

  const {
    data: rideRequests,
    isPending: ridesPending,
    error: ridesError,
    refetch: refetchRides,
    isFetching: ridesFetching,
  } = useCarpoolRideRequests(match.id)

  async function reload(): Promise<void> {
    // Bredda till hela carpool-prefixet (`#495`): en ändring här rör både den här matchens
    // erbjudanden (['carpool', matchId]), dess skjutsförfrågningar och tränarens lagöversikt
    // (['carpool','team',slug]). Utan det ser en tränare med båda vyerna öppna ett inaktuellt läge.
    await queryClient.invalidateQueries({ queryKey: ['carpool'] })
  }

  return (
    <section className="carpool" aria-labelledby="samakning">
      <h2 id="samakning" className="carpool__heading">
        Samåkning
      </h2>

      {/* ---- Erbjudanden: en förare som kör ------------------------------------------ */}
      <div className="carpool__group" aria-labelledby="samakning-erbjudanden">
        <h3 id="samakning-erbjudanden" className="carpool__group-heading">
          Erbjuden skjuts
        </h3>

        {offersPending && (
          <p className="carpool__empty" role="status">
            Hämtar erbjudandena…
          </p>
        )}

        {offersError !== null && !offersPending && (
          <div className="state state--error" role="alert">
            <p>{offersErrorMessage(offersError)}</p>
            <button
              type="button"
              className="button"
              disabled={offersFetching}
              onClick={() => {
                void refetchOffers()
              }}
            >
              {offersFetching ? 'Försöker…' : 'Försök igen'}
            </button>
          </div>
        )}

        {offers !== undefined && offers.length === 0 && (
          <p className="carpool__empty">Ingen har erbjudit skjuts till den här matchen än.</p>
        )}

        {offers !== undefined && offers.length > 0 && (
          <ul className="carpool__offers">
            {offers.map((offer) => (
              <CarpoolOfferCard
                key={offer.id}
                matchId={match.id}
                offer={offer}
                onChanged={reload}
              />
            ))}
          </ul>
        )}

        {/*
          Ingen gäst-variant: hela händelsesidan kräver inloggning (§KM.3, `#242`), så den som ser
          samåkningen är alltid medlem.
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
      </div>

      {/* ---- Förfrågningar: en förälder som behöver skjuts (`#63`) -------------------- */}
      <div className="carpool__group" aria-labelledby="samakning-behover">
        <h3 id="samakning-behover" className="carpool__group-heading">
          Behöver skjuts
        </h3>

        {ridesPending && (
          <p className="carpool__empty" role="status">
            Hämtar skjutsförfrågningarna…
          </p>
        )}

        {ridesError !== null && !ridesPending && (
          <div className="state state--error" role="alert">
            <p>{ridesErrorMessage(ridesError)}</p>
            <button
              type="button"
              className="button"
              disabled={ridesFetching}
              onClick={() => {
                void refetchRides()
              }}
            >
              {ridesFetching ? 'Försöker…' : 'Försök igen'}
            </button>
          </div>
        )}

        {rideRequests !== undefined && rideRequests.length === 0 && (
          <p className="carpool__empty">Ingen har bett om skjuts till den här matchen än.</p>
        )}

        {rideRequests !== undefined && rideRequests.length > 0 && (
          <ul className="carpool__offers">
            {rideRequests.map((request) => (
              <CarpoolRideRequestCard
                key={request.id}
                matchId={match.id}
                request={request}
                onChanged={reload}
              />
            ))}
          </ul>
        )}

        {asking ? (
          <CarpoolRideRequestForm
            onSubmit={async (input) => {
              await createRideRequest(match.id, input)
              setAsking(false)
              await reload()
            }}
            onCancel={() => {
              setAsking(false)
            }}
          />
        ) : (
          <div className="actions">
            <button
              type="button"
              className="button"
              onClick={() => {
                setAsking(true)
              }}
            >
              Fråga om skjuts
            </button>
          </div>
        )}
      </div>
    </section>
  )
}

function offersErrorMessage(error: unknown): string {
  return error instanceof ApiError && error.offline
    ? 'Ingen anslutning. Erbjudandena kan inte hämtas just nu — resten av matchen står kvar.'
    : 'Kunde inte hämta erbjudandena just nu.'
}

function ridesErrorMessage(error: unknown): string {
  return error instanceof ApiError && error.offline
    ? 'Ingen anslutning. Skjutsförfrågningarna kan inte hämtas just nu — resten av matchen står kvar.'
    : 'Kunde inte hämta skjutsförfrågningarna just nu.'
}
