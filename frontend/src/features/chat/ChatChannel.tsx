import { useState } from 'react'

import { ApiError } from '@/lib/api'
import { formatKickoffTime, formatMatchDate } from '@/lib/time'

import type { ChatChannel as Channel, ChatMessage } from './chatApi'
import {
  useCancelScheduled,
  useChatMessages,
  useAdminDeleteMessage,
  useDeleteMessage,
  usePost,
  useReport,
  useReports,
  useScheduled,
} from './useChat'

function messageOf(error: unknown): string {
  if (error instanceof ApiError) {
    return error.offline ? 'Ingen anslutning. Försök igen.' : error.message
  }

  return 'Något gick fel. Försök igen om en stund.'
}

function whenText(iso: string): string {
  return `${formatMatchDate(iso)} ${formatKickoffTime(iso)}`
}

/**
 * Så många skilda anmälningar ett meddelande behöver innan truppens admin kan radera det
 * (speglar serverns `ChatService.RemovalReportThreshold`, `#263`). En admin tystar inte en
 * enskild röst på egen hand. Servern är sanningen — knappen här speglar bara den.
 */
const REMOVAL_THRESHOLD = 3

/**
 * En chatt-kanal (§KM.1/§KM.10): trupp-chatten (`#201`) eller ett lag (`#202`). Medlemmar
 * läser/skriver; ledare kan schemalägga; en admin ser anmälningskön (bara på trupp-nivå —
 * moderering delas mellan kanalerna). Barn nämns aldrig av oss här.
 */
export function ChatChannel({
  channel,
  isLeader,
  isAdmin,
  myAccountId,
}: {
  channel: Channel
  isLeader: boolean
  isAdmin: boolean
  myAccountId: string | null
}) {
  const messages = useChatMessages(channel)
  const scheduled = useScheduled(channel, isLeader)

  // Anmälningskön är trupp-övergripande; den visas bara på trupp-sidan.
  const showReports = channel.kind === 'trupp' && isAdmin
  const truppId = channel.kind === 'trupp' ? channel.truppId : null
  const reports = useReports(truppId, showReports)

  // Adminens radering ur kön går via trupp-admin-endpointen (kanalobunden) — den vanliga
  // radera-knappen på ett meddelande använder medlemskanalen (`remove`).
  const adminRemove = useAdminDeleteMessage(truppId ?? '')

  const post = usePost(channel)
  const remove = useDeleteMessage(channel)
  const cancel = useCancelScheduled(channel)
  const report = useReport(channel)

  const [body, setBody] = useState('')
  const [scheduling, setScheduling] = useState(false)
  const [when, setWhen] = useState('')
  const [failure, setFailure] = useState<string | null>(null)

  // Anmälan: vilket meddelande formuläret är öppet för, motiveringstexten, och senast kvitterade.
  const [reportingId, setReportingId] = useState<string | null>(null)
  const [reportText, setReportText] = useState('')
  const [reportedId, setReportedId] = useState<string | null>(null)

  const run = (action: Promise<unknown>) => {
    setFailure(null)
    void action.catch((error: unknown) => setFailure(messageOf(error)))
  }

  function openReport(messageId: string): void {
    setFailure(null)
    setReportedId(null)
    setReportText('')
    setReportingId((current) => (current === messageId ? null : messageId))
  }

  function submitReport(event: React.FormEvent, messageId: string): void {
    event.preventDefault()

    const reason = reportText.trim()

    if (reason === '') {
      return
    }

    setFailure(null)
    report.mutate(
      { id: messageId, reason },
      {
        onSuccess: () => {
          setReportingId(null)
          setReportText('')
          setReportedId(messageId)
        },
        onError: (error: unknown) => setFailure(messageOf(error)),
      },
    )
  }

  function submit(event: React.FormEvent): void {
    event.preventDefault()

    const text = body.trim()

    if (text === '') {
      return
    }

    const publishAt = scheduling && when !== '' ? new Date(when).toISOString() : undefined

    setFailure(null)
    post.mutate(publishAt === undefined ? { body: text } : { body: text, publishAt }, {
      onSuccess: () => {
        setBody('')
        setWhen('')
        setScheduling(false)
      },
      onError: (error: unknown) => setFailure(messageOf(error)),
    })
  }

  // Bara den egna raden går att ta bort här. Admin raderar andras enbart ur anmälningskön,
  // och först vid tröskeln (`#263`) — inte med en knapp på varje meddelande.
  function canDelete(message: ChatMessage): boolean {
    return myAccountId !== null && message.authorAccountId === myAccountId
  }

  return (
    <section className="admin-section" aria-labelledby="chatt-rubrik">
      <h2 id="chatt-rubrik" className="visually-hidden">
        Meddelanden
      </h2>

      {messages.isLoading && <p className="state">Hämtar meddelanden…</p>}
      {messages.isError && (
        <p className="state state--error" role="alert">
          Kunde inte hämta meddelandena.
        </p>
      )}

      {messages.data && (
        <ul className="admin-list chat-messages">
          {messages.data.length === 0 && <li className="state">Inga meddelanden än.</li>}
          {messages.data.map((message) => (
            <li
              key={message.id}
              className={
                myAccountId !== null && message.authorAccountId === myAccountId
                  ? 'admin-list__row chat-msg chat-msg--own'
                  : 'admin-list__row chat-msg'
              }
            >
              <span>
                <strong>{message.authorName ?? 'Okänd'}</strong>{' '}
                <span className="admin-muted">{whenText(message.publishedUtc)}</span>
                <br />
                {message.deleted ? (
                  <em className="admin-muted">[borttaget]</em>
                ) : (
                  <span>{message.body}</span>
                )}
              </span>
              {!message.deleted && (
                <span className="actions">
                  <button
                    type="button"
                    className="button button--small"
                    aria-expanded={reportingId === message.id}
                    onClick={() => openReport(message.id)}
                  >
                    Anmäl
                  </button>
                  {canDelete(message) && (
                    <button
                      type="button"
                      className="button button--small"
                      disabled={remove.isPending}
                      onClick={() => run(remove.mutateAsync(message.id))}
                    >
                      Ta bort
                    </button>
                  )}
                </span>
              )}

              {reportingId === message.id && (
                <form className="chat-report" onSubmit={(event) => submitReport(event, message.id)}>
                  <label htmlFor={`report-${message.id}`}>Varför anmäler du meddelandet?</label>
                  <textarea
                    id={`report-${message.id}`}
                    rows={2}
                    maxLength={500}
                    value={reportText}
                    onChange={(event) => setReportText(event.target.value)}
                  />
                  <div className="actions">
                    <button
                      type="submit"
                      className="button button--small"
                      disabled={report.isPending || reportText.trim() === ''}
                    >
                      Skicka anmälan
                    </button>
                    <button
                      type="button"
                      className="button button--small"
                      onClick={() => {
                        setReportingId(null)
                        setReportText('')
                      }}
                    >
                      Avbryt
                    </button>
                  </div>
                </form>
              )}

              {reportedId === message.id && (
                <p className="chat-report__done" role="status">
                  Tack — meddelandet är anmält till truppens admin.
                </p>
              )}
            </li>
          ))}
        </ul>
      )}

      <form className="form" noValidate onSubmit={submit}>
        <div className="form__field">
          <label htmlFor="chatt-meddelande">Skriv ett meddelande</label>
          <textarea
            id="chatt-meddelande"
            rows={3}
            maxLength={2000}
            value={body}
            onChange={(event) => setBody(event.target.value)}
          />
        </div>

        {isLeader && (
          <>
            <label className="chatt-schedule">
              <input
                type="checkbox"
                checked={scheduling}
                onChange={(event) => setScheduling(event.target.checked)}
              />{' '}
              Schemalägg i stället för att skicka nu
            </label>

            {scheduling && (
              <div className="form__field">
                <label htmlFor="chatt-tid">Skicka vid</label>
                <input
                  id="chatt-tid"
                  type="datetime-local"
                  value={when}
                  onChange={(event) => setWhen(event.target.value)}
                />
              </div>
            )}
          </>
        )}

        {failure !== null && (
          <p className="state state--error" role="alert">
            {failure}
          </p>
        )}

        <div className="actions">
          <button type="submit" className="button" disabled={post.isPending}>
            {scheduling ? 'Schemalägg' : 'Skicka'}
          </button>
        </div>
      </form>

      {isLeader && scheduled.data && scheduled.data.length > 0 && (
        <div className="admin-subsection">
          <h3>Schemalagda meddelanden</h3>
          <ul className="admin-list">
            {scheduled.data.map((item) => (
              <li key={item.id} className="admin-list__row">
                <span>
                  <span className="admin-muted">{whenText(item.publishAtUtc)}</span>
                  <br />
                  {item.body}
                </span>
                <button
                  type="button"
                  className="button button--small"
                  disabled={cancel.isPending}
                  onClick={() => run(cancel.mutateAsync(item.id))}
                >
                  Ta bort
                </button>
              </li>
            ))}
          </ul>
        </div>
      )}

      {showReports && reports.data && reports.data.length > 0 && (
        <div className="admin-subsection">
          <h3>Anmälda meddelanden</h3>
          <ul className="admin-list">
            {reports.data.map((item) => {
              const canRemove = item.reportCount >= REMOVAL_THRESHOLD

              return (
                <li key={item.messageId} className="admin-list__row">
                  <span>
                    <strong>{item.authorName ?? 'Okänd'}</strong>{' '}
                    <span className="admin-muted">
                      {item.reportCount} anmälning{item.reportCount === 1 ? '' : 'ar'}
                    </span>
                    <br />
                    {item.deleted ? <em className="admin-muted">[borttaget]</em> : item.body}
                    {item.reasons.length > 0 && (
                      <ul className="chat-report__reasons">
                        {item.reasons.map((r) => (
                          <li key={`${r.reason}-${r.reportedUtc}`}>
                            <span className="admin-muted">{whenText(r.reportedUtc)}:</span>{' '}
                            {r.reason}
                          </li>
                        ))}
                      </ul>
                    )}
                  </span>
                  {!item.deleted &&
                    (canRemove ? (
                      <button
                        type="button"
                        className="button button--small"
                        disabled={adminRemove.isPending}
                        onClick={() => run(adminRemove.mutateAsync(item.messageId))}
                      >
                        Ta bort
                      </button>
                    ) : (
                      <span className="admin-muted chat-report__gate">
                        Kan tas bort när minst {REMOVAL_THRESHOLD} anmälningar kommit in.
                      </span>
                    ))}
                </li>
              )
            })}
          </ul>
        </div>
      )}
    </section>
  )
}
