import { useRef, useState } from 'react'

import { ConfirmButton } from '@/components/ConfirmButton'
import { ApiError } from '@/lib/api'
import { formatKickoffTime, formatMatchDate } from '@/lib/time'

import type { ChatChannel as Channel, ChatMessage } from './chatApi'
import { MessageMenu } from './MessageMenu'
import { MessageReactions } from './MessageReactions'
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

/** Initialen till avataren. Bara en dekorativ bokstav — namnet står ändå i bubblan. */
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

  // Vilket meddelandes val-meny som är öppen, och en timer för långtryck (touch-genväg).
  const [menuFor, setMenuFor] = useState<string | null>(null)
  const pressTimer = useRef<number | null>(null)
  // Bubblan som öppnade menyn — fokus flyttas tillbaka dit när menyn stängs (WCAG 2.4.3, #384).
  const triggerRef = useRef<HTMLDivElement | null>(null)

  function closeMenu(): void {
    setMenuFor(null)
    triggerRef.current?.focus()
  }

  // Meddelandet man håller på att svara på (tråd-svar), eller null.
  const [replyingTo, setReplyingTo] = useState<ChatMessage | null>(null)

  function clearPress(): void {
    if (pressTimer.current !== null) {
      window.clearTimeout(pressTimer.current)
      pressTimer.current = null
    }
  }

  function startPress(event: React.PointerEvent, messageId: string): void {
    // Långtryck är touch-genvägen. Mus öppnar menyn med högerklick, tangentbord med Enter/Space
    // på bubblan (som är en knapp) — därför ignoreras mus-långtryck här.
    if (event.pointerType === 'mouse') {
      return
    }

    pressTimer.current = window.setTimeout(() => setMenuFor(messageId), 500)
  }

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
    post.mutate(
      {
        body: text,
        ...(publishAt === undefined ? {} : { publishAt }),
        ...(replyingTo === null ? {} : { replyToMessageId: replyingTo.id }),
      },
      {
        onSuccess: () => {
          setBody('')
          setWhen('')
          setScheduling(false)
          setReplyingTo(null)
        },
        onError: (error: unknown) => setFailure(messageOf(error)),
      },
    )
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

      {/*
        Krypterings-notisen. Ärlig: meddelanden lagras krypterade (at-rest) och skickas över TLS —
        inte end-to-end, för truppens admin måste kunna läsa ett anmält meddelande (§KM.7).
      */}
      <p className="chat-encryption-note">
        <span className="chat-encryption-note__lock" aria-hidden="true">
          🔒
        </span>{' '}
        Krypterad chatt — meddelandena lagras krypterade
      </p>

      {messages.isLoading && <p className="state">Hämtar meddelanden…</p>}
      {messages.isError && (
        <p className="state state--error" role="alert">
          {messages.error instanceof ApiError && messages.error.offline
            ? 'Ingen anslutning. Kontrollera nätet och försök igen.'
            : 'Kunde inte hämta meddelandena.'}
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
                  ? 'chat-msg chat-msg--own'
                  : 'chat-msg'
              }
            >
              <div className="chat-msg__body">
                <div
                  className="chat-msg__bubble"
                  role={message.deleted ? undefined : 'button'}
                  tabIndex={message.deleted ? undefined : 0}
                  aria-haspopup={message.deleted ? undefined : 'menu'}
                  aria-expanded={message.deleted ? undefined : menuFor === message.id}
                  onPointerDown={
                    message.deleted ? undefined : (event) => startPress(event, message.id)
                  }
                  onPointerUp={clearPress}
                  onPointerLeave={clearPress}
                  onPointerCancel={clearPress}
                  onContextMenu={
                    message.deleted
                      ? undefined
                      : (event) => {
                          // Höger­klick (och långtryck på vissa enheter) öppnar menyn — ersätter
                          // den borttagna ⋯-knappen för mus.
                          event.preventDefault()
                          triggerRef.current = event.currentTarget
                          setMenuFor(message.id)
                        }
                  }
                  onKeyDown={
                    message.deleted
                      ? undefined
                      : (event) => {
                          // Tangentbord: Enter/Mellanslag öppnar/stänger menyn (bubblan är en knapp).
                          if (event.key === 'Enter' || event.key === ' ') {
                            event.preventDefault()
                            triggerRef.current = event.currentTarget
                            setMenuFor((current) => (current === message.id ? null : message.id))
                          }
                        }
                  }
                >
                  {message.replyTo && (
                    <span className="chat-msg__quote">
                      <span className="chat-msg__quote-author">
                        {message.replyTo.authorName ?? 'Okänd'}
                      </span>
                      <span className="chat-msg__quote-text">
                        {message.replyTo.deleted ? '[borttaget]' : message.replyTo.snippet}
                      </span>
                    </span>
                  )}
                  <span className="chat-msg__who">{message.authorName ?? 'Okänd'}</span>
                  {message.deleted ? (
                    <em className="chat-msg__text admin-muted">[borttaget]</em>
                  ) : (
                    <span className="chat-msg__text">{message.body}</span>
                  )}
                </div>

                {/* Reaktionerna hänger direkt under bubblan — inte bredvid tiden (`#redesign`). */}
                {!message.deleted && <MessageReactions message={message} channel={channel} />}

                <p className="chat-msg__meta">
                  <time dateTime={message.publishedUtc}>{whenText(message.publishedUtc)}</time>
                </p>

                {menuFor === message.id && !message.deleted && (
                  <MessageMenu
                    message={message}
                    channel={channel}
                    canDelete={canDelete(message)}
                    deleting={remove.isPending}
                    onReply={() => setReplyingTo(message)}
                    onReport={() => openReport(message.id)}
                    onDelete={() => run(remove.mutateAsync(message.id))}
                    onClose={closeMenu}
                  />
                )}

                {reportingId === message.id && (
                  <form
                    className="chat-report"
                    onSubmit={(event) => submitReport(event, message.id)}
                  >
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
              </div>
            </li>
          ))}
        </ul>
      )}

      <form className="form" noValidate onSubmit={submit}>
        {replyingTo !== null && (
          <div className="chat-reply-bar">
            <span className="chat-reply-bar__quote">
              <span className="chat-reply-bar__label">
                Svar till {replyingTo.authorName ?? 'Okänd'}
              </span>
              <span className="chat-reply-bar__text">
                {replyingTo.deleted ? '[borttaget]' : replyingTo.body}
              </span>
            </span>
            <button
              type="button"
              className="chat-reply-bar__cancel"
              aria-label="Avbryt svaret"
              onClick={() => setReplyingTo(null)}
            >
              <span aria-hidden="true">×</span>
            </button>
          </div>
        )}

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
          <button type="submit" className="button button--action" disabled={post.isPending}>
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
                <ConfirmButton
                  label="Ta bort"
                  className="button button--small"
                  confirmClassName="button button--small button--danger"
                  disabled={cancel.isPending}
                  onConfirm={() => run(cancel.mutateAsync(item.id))}
                />
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
                      <ConfirmButton
                        label="Ta bort"
                        className="button button--small"
                        confirmClassName="button button--small button--danger"
                        disabled={adminRemove.isPending}
                        onConfirm={() => run(adminRemove.mutateAsync(item.messageId))}
                      />
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
