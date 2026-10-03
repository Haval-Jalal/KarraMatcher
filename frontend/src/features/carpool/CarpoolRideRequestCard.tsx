import { useState } from 'react'

import { useAutoFocus } from '@/hooks/useAutoFocus'
import { ApiError } from '@/lib/api'

import {
  acceptRideOffer,
  denyRideOffer,
  offerSeat,
  retractRideOffer,
  withdrawRideRequest,
  type CarpoolRideOfferInput,
  type CarpoolRideRequest,
} from './carpoolApi'
import { rideDirectionLabel, seatCountLabel } from './carpoolLabels'
import { CarpoolRideOfferForm } from './CarpoolRideOfferForm'
import { CarpoolRideOfferList } from './CarpoolRideOfferList'
import { useCarpoolRideOffers } from './useCarpool'

/**
 * En förälders begäran om skjuts (§KM.12, `#63`).
 *
 * <h3>Spegelbilden av erbjudandekortet</h3>
 *
 * Här är det en förälder som behöver skjuts, och förare som räcker upp handen. Den som frågade ser
 * platserbjudandena och svarar på dem; en förare ser sitt eget. Hela sidan kräver inloggning (§KM.3),
 * så den som ser kortet är alltid medlem i laget.
 */
export function CarpoolRideRequestCard({
  matchId,
  request,
  onChanged,
}: {
  matchId: string
  request: CarpoolRideRequest
  onChanged: () => Promise<void>
}) {
  const [offering, setOffering] = useState(false)
  const [confirmWithdraw, setConfirmWithdraw] = useState(false)
  // "Dra tillbaka"-knappen ersätts av bekräftelsen; flytta fokus dit (WCAG 2.4.3, #598).
  const withdrawRef = useAutoFocus<HTMLParagraphElement>(confirmWithdraw)

  const {
    data: offers,
    isPending,
    error,
    refetch,
    isFetching,
  } = useCarpoolRideOffers(matchId, request.id, true)

  const mine = offers ?? []

  /*
   * Ett återtaget eller nekat erbjudande hindrar inte ett nytt. Servern tillåter bara ett levande
   * åt gången, och det är precis den här listan speglar.
   */
  const hasLiveOffer = mine.some(
    (offer) => offer.isMine && (offer.status === 'Pending' || offer.status === 'Accepted'),
  )

  // Erbjudandena kunde inte hämtas: visa fel + försök igen i stället för att tyst visa "ingen har
  // erbjudit plats" (den som frågade missar väntande erbjudanden) eller felaktigt erbjuda "Erbjud
  // plats" fast ett eget erbjudande redan finns.
  const offersError =
    error !== null ? (
      <div className="state state--error" role="alert">
        <p>
          {error instanceof ApiError && error.offline
            ? 'Ingen anslutning. Platserbjudandena kan inte hämtas just nu.'
            : 'Kunde inte hämta platserbjudandena just nu.'}
        </p>
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
    ) : null

  return (
    <li className="carpool-card carpool-card--request">
      <p className="carpool-card__lead">
        <span className="carpool-card__direction">{rideDirectionLabel(request.direction)}</span>
        <span className="carpool-card__seats">{seatCountLabel(request.seats)}</span>
      </p>

      {/*
        Vem som frågar. "Du frågar" för den egna raden. Saknas namnet är kontot skapat innan namnen
        fanns; då sägs ingenting hellre än "okänd", som låter som ett fel.
      */}
      {request.isMine ? (
        <p className="carpool-card__driver">Du frågar om skjuts</p>
      ) : (
        request.requesterName !== null && (
          <p className="carpool-card__driver">{request.requesterName} frågar om skjuts</p>
        )
      )}

      {request.note !== null && <p className="carpool__message">”{request.note}”</p>}

      {request.isMine && (
        <>
          <h3 className="carpool__subheading">Platserbjudanden</h3>

          {offersError ?? (
            <CarpoolRideOfferList
              offers={mine}
              isRequester
              onAccept={async (offer) => {
                await acceptRideOffer(matchId, offer.id, null)
                await onChanged()
              }}
              onDeny={async (offer, message) => {
                await denyRideOffer(matchId, offer.id, message)
                await onChanged()
              }}
              onRetract={async (offer) => {
                await retractRideOffer(matchId, offer.id)
                await onChanged()
              }}
            />
          )}

          {confirmWithdraw ? (
            <div className="actions">
              <p className="carpool__confirm" tabIndex={-1} ref={withdrawRef}>
                Dra tillbaka förfrågan?
              </p>
              <button
                type="button"
                className="button button--danger"
                onClick={() => {
                  void (async () => {
                    await withdrawRideRequest(matchId, request.id)
                    setConfirmWithdraw(false)
                    await onChanged()
                  })()
                }}
              >
                Ja, dra tillbaka
              </button>
              <button
                type="button"
                className="button button--action"
                onClick={() => {
                  setConfirmWithdraw(false)
                }}
              >
                Behåll
              </button>
            </div>
          ) : (
            <div className="actions">
              <button
                type="button"
                className="button button--action"
                onClick={() => {
                  setConfirmWithdraw(true)
                }}
              >
                Dra tillbaka
              </button>
            </div>
          )}
        </>
      )}

      {!request.isMine && (
        <>
          {offersError ?? (
            <CarpoolRideOfferList
              offers={mine}
              isRequester={false}
              onRetract={async (offer) => {
                await retractRideOffer(matchId, offer.id)
                await onChanged()
              }}
            />
          )}

          {/* Vänta tills det egna erbjudandet hämtats innan knappen visas — annars blinkar "Erbjud
              plats" förbi medan listan ännu är tom. Vid fel visas felrutan ovan i stället för en
              knapp vi inte kan lita på. */}
          {error === null &&
            !isPending &&
            !hasLiveOffer &&
            (offering ? (
              <CarpoolRideOfferForm
                rideRequestId={request.id}
                onSubmit={async (input: CarpoolRideOfferInput) => {
                  await offerSeat(matchId, request.id, input)
                  setOffering(false)
                  await onChanged()
                }}
                onCancel={() => {
                  setOffering(false)
                }}
              />
            ) : (
              <div className="actions">
                <button
                  type="button"
                  className="button"
                  onClick={() => {
                    setOffering(true)
                  }}
                >
                  Erbjud plats
                </button>
              </div>
            ))}
        </>
      )}
    </li>
  )
}
