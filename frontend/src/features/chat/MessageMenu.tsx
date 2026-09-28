import { useEffect, useRef } from 'react'

import { REACTION_EMOJIS, reactionLabel, type ChatChannel, type ChatMessage } from './chatApi'
import { useToggleReaction } from './useChat'

/**
 * Val-menyn för ett meddelande (`#redesign`): öppnas genom att hålla in meddelandet (touch) eller
 * via ⋯-knappen (mus/tangentbort). Ersätter de tidigare alltid-synliga knapparna, som gjorde varje
 * rad plottrig — som i Teams/WhatsApp ligger valen bakom en gest i stället.
 *
 * <h3>Vad som finns här</h3>
 *
 * Emoji-reaktioner överst, sedan Kopiera, Anmäl och (för eget meddelande) Ta bort. Svara-i-tråd
 * kommer i ett senare steg. Privat DM byggs inte — kontakten hålls i den modererade kanalen.
 *
 * <h3>Tillgänglighet</h3>
 *
 * Menyn öppnas även med ⋯-knappen, så den når tangentbord och skärmläsare (långtryck är bara en
 * touch-genväg). Escape och klick utanför stänger; fokus flyttas in i menyn när den öppnas.
 */
export function MessageMenu({
  message,
  channel,
  canDelete,
  deleting,
  onReport,
  onDelete,
  onClose,
}: {
  message: ChatMessage
  channel: ChatChannel
  canDelete: boolean
  deleting: boolean
  onReport: () => void
  onDelete: () => void
  onClose: () => void
}) {
  const toggle = useToggleReaction(channel)
  const panelRef = useRef<HTMLDivElement>(null)

  useEffect(() => {
    panelRef.current?.focus()

    function onKey(event: KeyboardEvent): void {
      if (event.key === 'Escape') {
        onClose()
      }
    }

    document.addEventListener('keydown', onKey)

    return () => {
      document.removeEventListener('keydown', onKey)
    }
  }, [onClose])

  async function copy(): Promise<void> {
    try {
      await navigator.clipboard.writeText(message.body)
    } catch {
      // Urklipp kan vara blockerat (behörighet, äldre webbläsare) — tyst, menyn stängs ändå.
    }

    onClose()
  }

  return (
    <>
      {/* Klick utanför stänger. En knapp och inte en div, så den når tangentbordet. */}
      <button
        type="button"
        className="msg-menu__backdrop"
        aria-label="Stäng menyn"
        onClick={onClose}
      />

      <div className="msg-menu" aria-label="Val för meddelandet" tabIndex={-1} ref={panelRef}>
        <div className="msg-menu__emojis" role="group" aria-label="Reagera">
          {REACTION_EMOJIS.map((emoji) => (
            <button
              key={emoji}
              type="button"
              className="msg-menu__emoji"
              aria-label={reactionLabel[emoji] ?? emoji}
              disabled={toggle.isPending}
              onClick={() => {
                toggle.mutate({ messageId: message.id, emoji })
                onClose()
              }}
            >
              <span aria-hidden="true">{emoji}</span>
            </button>
          ))}
        </div>

        <button
          type="button"
          className="msg-menu__item"
          onClick={() => {
            void copy()
          }}
        >
          Kopiera
        </button>

        <button
          type="button"
          className="msg-menu__item"
          onClick={() => {
            onReport()
            onClose()
          }}
        >
          Anmäl
        </button>

        {canDelete && (
          <button
            type="button"
            className="msg-menu__item msg-menu__item--danger"
            disabled={deleting}
            onClick={() => {
              onDelete()
              onClose()
            }}
          >
            Ta bort
          </button>
        )}
      </div>
    </>
  )
}
