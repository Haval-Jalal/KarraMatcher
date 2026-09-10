import { useState } from 'react'

import { ApiError } from '@/lib/api'

import type { CarpoolRequest } from './carpoolApi'
import { CarpoolDenyForm } from './CarpoolDenyForm'
import { requestStatusLabel, seatCountLabel } from './carpoolLabels'

/**
 * Förfrågningarna på ett erbjudande.
 *
 * <h3>Två läsare, en lista</h3>
 *
 * Föraren ser alla och svarar på dem. Alla andra ser bara sin egen — och den
 * filtreringen sker i servern, inte här (§KM.12). Listan visar alltså bara det den fått,
 * och behöver inte veta vem som tittar för att hålla fritexten borta från fel ögon.
 *
 * <h3>Ingen har ett namn</h3>
 *
 * Appen lagrar inget namn på den som frågar, så det finns inget att visa. Det är inte en
 * lucka: överenskommelsen sker i meddelandet, och den som vill säga vem hen är skriver
 * det själv.
 */
export function CarpoolRequestList({
  requests,
  isDriver,
  onAccept,
  onDeny,
  onRetract,
}: {
  requests: CarpoolRequest[]
  isDriver: boolean
  /** Bara föraren svarar, så de här saknas för alla andra. */
  onAccept?: ((request: CarpoolRequest) => Promise<void>) | undefined
  onDeny?: ((request: CarpoolRequest, message: string) => Promise<void>) | undefined
  onRetract: (request: CarpoolRequest) => Promise<void>
}) {
  if (requests.length === 0) {
    return isDriver ? <p className="carpool__empty">Ingen har frågat än.</p> : null
  }

  return (
    <ul className="carpool__requests">
      {requests.map((request) => (
        <li key={request.id} className="carpool__request">
          <RequestRow
            request={request}
            isDriver={isDriver}
            onAccept={onAccept}
            onDeny={onDeny}
            onRetract={onRetract}
          />
        </li>
      ))}
    </ul>
  )
}

function RequestRow({
  request,
  isDriver,
  onAccept,
  onDeny,
  onRetract,
}: {
  request: CarpoolRequest
  isDriver: boolean
  /** Bara föraren svarar, så de här saknas för alla andra. */
  onAccept?: ((request: CarpoolRequest) => Promise<void>) | undefined
  onDeny?: ((request: CarpoolRequest, message: string) => Promise<void>) | undefined
  onRetract: (request: CarpoolRequest) => Promise<void>
}) {
  const [denying, setDenying] = useState(false)
  const [busy, setBusy] = useState(false)
  const [failure, setFailure] = useState<string | null>(null)

  const isPending = request.status === 'Pending'

  /**
   * Kör en åtgärd och håller knappen upptagen under tiden.
   *
   * Utan spärren går det att trycka två gånger på dålig täckning, och den andra
   * tryckningen möts av "förfrågan är redan besvarad" — ett fel som ser ut som appens.
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
        <strong>{leadFor(request)}</strong>{' '}
        <span className="carpool__request-seats">{seatCountLabel(request.seats)}</span>
      </p>

      {request.message !== null && <p className="carpool__message">”{request.message}”</p>}

      <p className="carpool__status">{requestStatusLabel(request.status)}</p>

      {/*
        Förarens svar visas för den som frågade. Det är hela poängen med att ett nekande
        kräver ord — de ska nå fram, inte bara sparas.
      */}
      {request.responseMessage !== null && (
        <p className="carpool__message">
          <span className="carpool__message-from">Förarens svar:</span> ”{request.responseMessage}”
        </p>
      )}

      {failure !== null && (
        <p className="state state--error" role="alert">
          {failure}
        </p>
      )}

      {isDriver && isPending && onAccept !== undefined && !denying && (
        <div className="actions">
          <button
            type="button"
            className="button"
            disabled={busy}
            onClick={() => {
              void run(() => onAccept(request))
            }}
          >
            Ja tack, häng med
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

      {isDriver && isPending && onDeny !== undefined && denying && (
        <CarpoolDenyForm
          requestId={request.id}
          onSubmit={async (message) => {
            await onDeny(request, message)
            setDenying(false)
          }}
          onCancel={() => {
            setDenying(false)
          }}
        />
      )}

      {!isDriver && request.isMine && isPending && (
        <div className="actions">
          <button
            type="button"
            className="button button--action"
            disabled={busy}
            onClick={() => {
              void run(() => onRetract(request))
            }}
          >
            Återta förfrågan
          </button>
        </div>
      )}
    </>
  )
}

/**
 * Vems förfrågan det är.
 *
 * Namnet när det finns — det är hela poängen med `#154`: föraren ska veta vem hen släpper
 * in i bilen. Saknas det är kontot skapat innan namnen fanns, och då säger raden vad den
 * vet i stället för att hitta på.
 */
function leadFor(request: CarpoolRequest): string {
  if (request.isMine) {
    return 'Din förfrågan'
  }

  return request.requesterName === null
    ? 'Frågar om skjuts'
    : `${request.requesterName} frågar om skjuts`
}

/**
 * Servern säger redan varför ett svar inte gick igenom — att platserna tagit slut, att
 * förfrågan hunnit besvaras, att den återtagits. De orden är bättre än något generellt,
 * så de visas som de är. Undantaget är ett anrop som aldrig nådde fram: då är det nätet
 * som fallerat, och serverns ord hade varit missvisande.
 */
function answerFailureText(error: unknown): string {
  if (error instanceof ApiError) {
    return error.offline
      ? 'Ingen anslutning. Svaret är inte skickat — försök igen när du har nät.'
      : error.message
  }

  return 'Svaret gick inte att skicka just nu. Försök igen om en stund.'
}
