import { useState } from 'react'

import { SignInLink } from '@/components/SignInLink'

import { formatDayAndMonth, formatKickoffTime } from '@/lib/time'

import {
  acceptRequest,
  askForSeat,
  denyRequest,
  retractRequest,
  withdrawOffer,
  type CarpoolOffer,
  type CarpoolRequestInput,
} from './carpoolApi'
import { directionLabel, seatsLabel } from './carpoolLabels'
import { CarpoolRequestForm } from './CarpoolRequestForm'
import { CarpoolRequestList } from './CarpoolRequestList'
import { useCarpoolRequests } from './useCarpool'

/**
 * Ett erbjudande om skjuts.
 *
 * <h3>Fullt är märkt, men inte stängt</h3>
 *
 * Ett fullt erbjudande står kvar i listan och går fortfarande att fråga om (§KM.12).
 * Märkningen finns för att ingen ska tro att platsen är klar, inte för att stänga dörren
 * — föraren ska kunna svara "någon annan hann före" med egna ord.
 *
 * <h3>Vem som ser vad</h3>
 *
 * Föraren ser förfrågningarna och svarar på dem. Den som frågat ser sin egen. En gäst ser
 * erbjudandet men inga förfrågningar alls — hämtningen är avstängd utan konto, så en gäst
 * kostar inte ett 401 i onödan.
 */
export function CarpoolOfferCard({
  matchId,
  offer,
  isSignedIn,
  onChanged,
}: {
  matchId: string
  offer: CarpoolOffer
  isSignedIn: boolean
  onChanged: () => Promise<void>
}) {
  const [asking, setAsking] = useState(false)
  const [confirmWithdraw, setConfirmWithdraw] = useState(false)

  const { data: requests } = useCarpoolRequests(matchId, offer.id, isSignedIn)

  const mine = requests ?? []

  /*
   * En återtagen eller nekad förfrågan hindrar inte en ny. Servern tillater bara en
   * levande at gangen, och det ar precis den har listan speglar.
   */
  const hasLiveRequest = mine.some(
    (request) => request.isMine && (request.status === 'Pending' || request.status === 'Accepted'),
  )

  return (
    <li className={offer.isFull ? 'carpool-card carpool-card--full' : 'carpool-card'}>
      <p className="carpool-card__lead">
        <span className="carpool-card__direction">{directionLabel(offer.direction)}</span>
        <span
          className={
            offer.isFull ? 'carpool-card__seats carpool-card__seats--full' : 'carpool-card__seats'
          }
        >
          {seatsLabel(offer)}
        </span>
      </p>

      <p className="carpool-card__when">
        Avgår {formatDayAndMonth(offer.departureUtc)} kl.{' '}
        <strong>{formatKickoffTime(offer.departureUtc)}</strong>
      </p>

      <p className="carpool-card__place">Från {offer.departurePlace}</p>

      {offer.note !== null && <p className="carpool__message">”{offer.note}”</p>}

      {offer.isMine && (
        <>
          <h3 className="carpool__subheading">Förfrågningar</h3>

          <CarpoolRequestList
            requests={mine}
            isDriver
            onAccept={async (request) => {
              await acceptRequest(matchId, request.id, null)
              await onChanged()
            }}
            onDeny={async (request, message) => {
              await denyRequest(matchId, request.id, message)
              await onChanged()
            }}
            onRetract={async (request) => {
              await retractRequest(matchId, request.id)
              await onChanged()
            }}
          />

          {confirmWithdraw ? (
            <div className="actions">
              <p className="carpool__confirm">Dra tillbaka erbjudandet?</p>
              <button
                type="button"
                className="button button--danger"
                onClick={() => {
                  void (async () => {
                    await withdrawOffer(matchId, offer.id)
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

      {/*
        Gästen får samma knapp som alla andra, men den går till inloggningen. Att dölja den
        hade varit ett tystare fel: erbjudandet syns, och då ser det ut som att det inte går
        att fråga om (`#54`, §KM.3).
      */}
      {!offer.isMine && !isSignedIn && (
        <div className="actions">
          <SignInLink variant="secondary">Logga in för att fråga om plats</SignInLink>
        </div>
      )}

      {!offer.isMine && isSignedIn && (
        <>
          <CarpoolRequestList
            requests={mine}
            isDriver={false}
            onRetract={async (request) => {
              await retractRequest(matchId, request.id)
              await onChanged()
            }}
          />

          {!hasLiveRequest &&
            (asking ? (
              <CarpoolRequestForm
                offerId={offer.id}
                isFull={offer.isFull}
                onSubmit={async (input: CarpoolRequestInput) => {
                  await askForSeat(matchId, offer.id, input)
                  setAsking(false)
                  await onChanged()
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
                  Fråga om plats
                </button>
              </div>
            ))}
        </>
      )}
    </li>
  )
}
