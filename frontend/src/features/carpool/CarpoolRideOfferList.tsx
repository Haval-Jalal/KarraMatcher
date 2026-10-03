import { useState } from 'react'

import { ConfirmButton } from '@/components/ConfirmButton'
import { ApiError } from '@/lib/api'

import type { CarpoolRideOffer } from './carpoolApi'
import { CarpoolDenyForm } from './CarpoolDenyForm'
import { requestStatusLabel, seatCountLabel } from './carpoolLabels'

/**
 * Platserbjudandena på en skjutsförfrågan (§KM.12, `#63`).
 *
 * <h3>Spegelbilden av förfrågningslistan</h3>
 *
 * Här är det den som frågade som svarar, och förare som erbjuder. Den som frågade ser alla
 * erbjudanden och tackar ja eller nej; en förare ser bara sitt eget och kan återta det. Precis
 * som åt andra hållet filtrerar servern på läsaren, så listan behöver inte veta vem som tittar för
 * att hålla fritexten borta från fel ögon.
 */
export function CarpoolRideOfferList({
  offers,
  isRequester,
  onAccept,
  onDeny,
  onRetract,
}: {
  offers: CarpoolRideOffer[]
  isRequester: boolean
  /** Bara den som frågade svarar, så de här saknas för en förare. */
  onAccept?: ((offer: CarpoolRideOffer) => Promise<void>) | undefined
  onDeny?: ((offer: CarpoolRideOffer, message: string) => Promise<void>) | undefined
  onRetract: (offer: CarpoolRideOffer) => Promise<void>
}) {
  if (offers.length === 0) {
    return isRequester ? (
      <p className="carpool__empty">Ingen förare har erbjudit plats än.</p>
    ) : null
  }

  return (
    <ul className="carpool__requests">
      {offers.map((offer) => (
        <li key={offer.id} className="carpool__request">
          <OfferRow
            offer={offer}
            isRequester={isRequester}
            onAccept={onAccept}
            onDeny={onDeny}
            onRetract={onRetract}
          />
        </li>
      ))}
    </ul>
  )
}

function OfferRow({
  offer,
  isRequester,
  onAccept,
  onDeny,
  onRetract,
}: {
  offer: CarpoolRideOffer
  isRequester: boolean
  onAccept?: ((offer: CarpoolRideOffer) => Promise<void>) | undefined
  onDeny?: ((offer: CarpoolRideOffer, message: string) => Promise<void>) | undefined
  onRetract: (offer: CarpoolRideOffer) => Promise<void>
}) {
  const [denying, setDenying] = useState(false)
  const [busy, setBusy] = useState(false)
  const [failure, setFailure] = useState<string | null>(null)

  const isPending = offer.status === 'Pending'

  /**
   * Kör en åtgärd och håller knappen upptagen under tiden.
   *
   * Utan spärren går det att trycka två gånger på dålig täckning, och den andra tryckningen möts
   * av "platserbjudandet är redan besvarat" — ett fel som ser ut som appens.
   */
  async function run(action: () => Promise<void>): Promise<void> {
    setBusy(true)

    try {
      await action()
      setFailure(null)
    } catch (error) {
      setFailure(answerFailureText(error))
    } finally {
      setBusy(false)
    }
  }

  return (
    <>
      <p className="carpool__request-lead">
        <strong>{leadFor(offer)}</strong>{' '}
        <span className="carpool__request-seats">{seatCountLabel(offer.seats)}</span>
      </p>

      {offer.message !== null && <p className="carpool__message">”{offer.message}”</p>}

      <p className="carpool__status">{requestStatusLabel(offer.status)}</p>

      {/*
        Svaret vid ett nekande visas för föraren. Det är hela poängen med att ett nekande kräver
        ord — de ska nå fram, inte bara sparas.
      */}
      {offer.responseMessage !== null && (
        <p className="carpool__message">
          <span className="carpool__message-from">Svar:</span> ”{offer.responseMessage}”
        </p>
      )}

      {failure !== null && (
        <p className="state state--error" role="alert">
          {failure}
        </p>
      )}

      {isRequester && isPending && onAccept !== undefined && !denying && (
        <div className="actions">
          <button
            type="button"
            className="button"
            disabled={busy}
            onClick={() => {
              void run(() => onAccept(offer))
            }}
          >
            Ja tack, jag åker med
          </button>
          <button
            type="button"
            className="button button--action"
            onClick={() => {
              setDenying(true)
            }}
          >
            Neka
          </button>
        </div>
      )}

      {isRequester && isPending && onDeny !== undefined && denying && (
        <CarpoolDenyForm
          requestId={offer.id}
          onSubmit={async (message) => {
            await onDeny(offer, message)
            setDenying(false)
          }}
          onCancel={() => {
            setDenying(false)
          }}
        />
      )}

      {!isRequester && offer.isMine && isPending && (
        <div className="actions">
          <ConfirmButton
            label="Återta erbjudande"
            disabled={busy}
            onConfirm={() => {
              void run(() => onRetract(offer))
            }}
          />
        </div>
      )}
    </>
  )
}

/**
 * Vems erbjudande det är.
 *
 * Namnet när det finns — den som frågade ska veta vems bil hen sätter sig i. Saknas det är kontot
 * skapat innan namnen fanns, och då säger raden vad den vet i stället för att hitta på.
 */
function leadFor(offer: CarpoolRideOffer): string {
  if (offer.isMine) {
    return 'Ditt erbjudande'
  }

  return offer.driverName === null
    ? 'En förare erbjuder plats'
    : `${offer.driverName} erbjuder plats`
}

/**
 * Servern säger redan varför ett svar inte gick igenom — att förfrågan hunnit lösas, att
 * erbjudandet redan besvarats. De orden är bättre än något generellt. Undantaget är ett anrop som
 * aldrig nådde fram: då är det nätet som fallerat, och serverns ord hade varit missvisande.
 */
function answerFailureText(error: unknown): string {
  if (error instanceof ApiError) {
    return error.offline
      ? 'Ingen anslutning. Svaret är inte skickat — försök igen när du har nät.'
      : error.message
  }

  return 'Svaret gick inte att skicka just nu. Försök igen om en stund.'
}
