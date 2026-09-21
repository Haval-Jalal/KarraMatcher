import { useState } from 'react'

import { ApiError } from '@/lib/api'
import { formatFullDate, formatKickoffTime, formatMatchDate } from '@/lib/time'

import {
  getAccountExport,
  type AccountExport,
  type AttendanceReplyCode,
  type CarpoolDirectionCode,
  type CarpoolOfferStatusCode,
  type CarpoolRequestStatusCode,
} from './dataExportApi'

/**
 * Registerutdraget så en förälder ser det (`#67`, §KM.6).
 *
 * <h3>Läsbart först, nedladdningsbart sen</h3>
 *
 * Utdraget visas på skärmen i vanlig svenska — det är "läsbart för en människa"-kravet. Den
 * som vill spara det får en fil på köpet. Servern lagrar så lite att det oftast ryms på en
 * skärm, och det är själva poängen: lite att lämna ut är ett gott tecken.
 *
 * <h3>Hämtas på begäran</h3>
 *
 * Inte vid sidladdning — ett registerutdrag är något man ber om, inte något som ska ligga
 * och lysa på kontosidan varje gång. Knappen hämtar det när föräldern vill se det.
 */
export function DataExportSection() {
  const [data, setData] = useState<AccountExport | null>(null)
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState<string | null>(null)

  async function handleLoad() {
    setLoading(true)
    setError(null)

    try {
      setData(await getAccountExport())
    } catch (caught) {
      setError(
        caught instanceof ApiError && caught.offline
          ? 'Ingen anslutning. Försök igen när du är uppkopplad.'
          : 'Det gick inte att hämta uppgifterna just nu. Försök igen om en stund.',
      )
    } finally {
      setLoading(false)
    }
  }

  return (
    <section aria-labelledby="mina-uppgifter">
      <h2 className="match-list__title" id="mina-uppgifter">
        Mina uppgifter
      </h2>

      <p className="state">
        Här kan du se allt vi har sparat om dig, och ladda ner det som en fil. Det är oftast lite —
        appen samlar så lite som möjligt.
      </p>

      <div className="actions">
        <button
          type="button"
          className="button button--action"
          onClick={() => void handleLoad()}
          disabled={loading}
        >
          {data === null ? 'Hämta mina uppgifter' : 'Uppdatera'}
        </button>

        {data !== null ? <DownloadButton data={data} /> : null}
      </div>

      <div aria-live="polite">
        {loading ? <p className="state">Hämtar dina uppgifter …</p> : null}
        {error !== null ? (
          <p className="state" role="alert">
            {error}
          </p>
        ) : null}
        {data !== null && !loading ? <ExportView data={data} /> : null}
      </div>
    </section>
  )
}

/** Laddar ner utdraget som en JSON-fil — den portabla, maskinläsbara formen. */
function DownloadButton({ data }: { data: AccountExport }) {
  function handleDownload() {
    const blob = new Blob([JSON.stringify(data, null, 2)], {
      type: 'application/json',
    })
    const url = URL.createObjectURL(blob)
    const anchor = document.createElement('a')

    anchor.href = url
    anchor.download = 'karra-matcher-mina-uppgifter.json'
    document.body.appendChild(anchor)
    anchor.click()
    anchor.remove()
    URL.revokeObjectURL(url)
  }

  return (
    <button type="button" className="button" onClick={handleDownload}>
      Ladda ner som fil
    </button>
  )
}

function ExportView({ data }: { data: AccountExport }) {
  return (
    <div className="prose">
      <p className="state">Utdrag skapat {formatFullDate(data.exportedUtc)}.</p>

      <h3>Ditt konto</h3>
      <ul>
        <li>E-post: {data.account.email}</li>
        <li>Namn: {fullName(data.account.firstName, data.account.lastName)}</li>
        <li>Konto skapat: {formatFullDate(data.account.createdUtc)}</li>
        <li>
          Senast inloggad:{' '}
          {data.account.lastSignedInUtc === null
            ? 'okänt'
            : formatFullDate(data.account.lastSignedInUtc)}
        </li>
      </ul>

      <h3>Dina samåkningserbjudanden</h3>
      {data.carpoolOffers.length === 0 ? (
        <p className="state">Du har inte lagt upp någon skjuts.</p>
      ) : (
        <ul>
          {data.carpoolOffers.map((offer, index) => (
            <li key={index}>
              {matchLabel(offer.matchOpponent, offer.matchKickoffUtc)} —{' '}
              {direction(offer.direction)}, från {offer.departurePlace}{' '}
              {formatKickoffTime(offer.departureUtc)}, {offer.seats} platser.{' '}
              {offerStatus(offer.status)}.
              {offer.note !== null && offer.note !== '' ? ` Notis: ${offer.note}` : ''}
            </li>
          ))}
        </ul>
      )}

      <h3>Dina åkförfrågningar</h3>
      {data.carpoolRequests.length === 0 ? (
        <p className="state">Du har inte skickat någon åkförfrågan.</p>
      ) : (
        <ul>
          {data.carpoolRequests.map((request, index) => (
            <li key={index}>
              {matchLabel(request.matchOpponent, request.matchKickoffUtc)} — {request.seats}{' '}
              platser. {requestStatus(request.status)}.
              {request.message !== null && request.message !== ''
                ? ` Din hälsning: ${request.message}`
                : ''}
              {request.responseMessage !== null && request.responseMessage !== ''
                ? ` Svar: ${request.responseMessage}`
                : ''}
            </li>
          ))}
        </ul>
      )}

      <h3>Dina närvarosvar</h3>
      {data.attendanceResponses.length === 0 ? (
        <p className="state">Du har inte svarat på någon kallelse.</p>
      ) : (
        <ul>
          {data.attendanceResponses.map((response, index) => (
            <li key={index}>
              {matchLabel(response.eventLabel, response.eventKickoffUtc)} — {response.childName}:{' '}
              {attendanceReply(response.reply)}.
            </li>
          ))}
        </ul>
      )}

      <h3>Dina notisinställningar</h3>
      {data.notificationSettings.length === 0 ? (
        <p className="state">Du har inte ändrat några notisinställningar.</p>
      ) : (
        <ul>
          {data.notificationSettings.map((setting, index) => (
            <li key={index}>
              {setting.teamName}: matchändringar {onOff(setting.matchChanges)}, samåkning{' '}
              {onOff(setting.carpool)}, påminnelser {onOff(setting.reminders)}.
            </li>
          ))}
        </ul>
      )}

      <h3>Dina notisprenumerationer</h3>
      {data.pushSubscriptions.length === 0 ? (
        <p className="state">Du har inga notisprenumerationer.</p>
      ) : (
        <ul>
          {data.pushSubscriptions.map((subscription, index) => (
            <li key={index}>
              {subscription.teamName} — sedan {formatFullDate(subscription.createdUtc)}. Vi sparar
              ingen adress som pekar ut dig, bara en teknisk kanal till din webbläsare.
            </li>
          ))}
        </ul>
      )}

      <h3>Spelarkortet</h3>
      <p>{data.playerCard.message}</p>
    </div>
  )
}

function fullName(first: string | null, last: string | null): string {
  const name = [first, last].filter((part) => part !== null && part !== '').join(' ')
  return name === '' ? 'Inget namn ifyllt' : name
}

function matchLabel(opponent: string, kickoffUtc: string): string {
  return `Match mot ${opponent}, ${formatMatchDate(kickoffUtc)} kl. ${formatKickoffTime(kickoffUtc)}`
}

function direction(code: CarpoolDirectionCode): string {
  switch (code) {
    case 'ToMatch':
      return 'till matchen'
    case 'FromMatch':
      return 'från matchen'
    case 'Both':
      return 'båda hållen'
  }
}

function offerStatus(code: CarpoolOfferStatusCode): string {
  return code === 'Open' ? 'Öppet' : 'Tillbakadraget'
}

function requestStatus(code: CarpoolRequestStatusCode): string {
  switch (code) {
    case 'Pending':
      return 'Väntar på svar'
    case 'Accepted':
      return 'Accepterad'
    case 'Denied':
      return 'Nekad'
    case 'Retracted':
      return 'Återtagen'
  }
}

function attendanceReply(code: AttendanceReplyCode): string {
  switch (code) {
    case 'Coming':
      return 'Ja'
    case 'NotComing':
      return 'Nej'
  }
}

function onOff(value: boolean): string {
  return value ? 'på' : 'av'
}
