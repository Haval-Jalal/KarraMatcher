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

  const run = (action: Promise<unknown>) => {
    setFailure(null)
    void action.catch((error: unknown) => setFailure(messageOf(error)))
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

  function canDelete(message: ChatMessage): boolean {
    return isAdmin || (myAccountId !== null && message.authorAccountId === myAccountId)
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
        <ul className="admin-list">
          {messages.data.length === 0 && <li className="state">Inga meddelanden än.</li>}
          {messages.data.map((message) => (
            <li key={message.id} className="admin-list__row">
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
                    disabled={report.isPending}
                    onClick={() => run(report.mutateAsync(message.id))}
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
            {reports.data.map((item) => (
              <li key={item.messageId} className="admin-list__row">
                <span>
                  <strong>{item.authorName ?? 'Okänd'}</strong>{' '}
                  <span className="admin-muted">
                    {item.reportCount} anmälning{item.reportCount === 1 ? '' : 'ar'}
                  </span>
                  <br />
                  {item.deleted ? <em className="admin-muted">[borttaget]</em> : item.body}
                </span>
                {!item.deleted && (
                  <button
                    type="button"
                    className="button button--small"
                    disabled={adminRemove.isPending}
                    onClick={() => run(adminRemove.mutateAsync(item.messageId))}
                  >
                    Ta bort
                  </button>
                )}
              </li>
            ))}
          </ul>
        </div>
      )}
    </section>
  )
}
