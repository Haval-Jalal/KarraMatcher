import { useEffect, useRef, useState } from 'react'

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
 * Bubblan är en knapp: tangentbord öppnar menyn med Enter/Mellanslag och mus med högerklick, så
 * den når fler än touch-långtrycket. Escape och klick utanför stänger; fokus flyttas in i menyn
 * när den öppnas och tillbaka till bubblan när den stängs (WCAG 2.4.3).
 */
export function MessageMenu({
  message,
  channel,
  canDelete,
  deleting,
  onReply,
  onReport,
  onDelete,
  onClose,
}: {
  message: ChatMessage
  channel: ChatChannel
  canDelete: boolean
  deleting: boolean
  onReply: () => void
  onReport: () => void
  onDelete: () => void
  onClose: () => void
}) {
  const toggle = useToggleReaction(channel)
  const panelRef = useRef<HTMLDivElement>(null)
  // Radering är oåterkallelig — kräv ett andra klick i menyn i stället för att ta bort direkt (`#383`).
  const [confirmingDelete, setConfirmingDelete] = useState(false)

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

  // Fokusfälla: Tab och Shift+Tab cirkulerar inom panelen i stället för att lämna den (WCAG 2.4.3
  // för en modal popover, #404). Escape och klick utanför stänger den fortfarande.
  function trapFocus(event: React.KeyboardEvent<HTMLDivElement>): void {
    if (event.key !== 'Tab') {
      return
    }

    const panel = panelRef.current
    if (panel === null) {
      return
    }

    const focusables = panel.querySelectorAll<HTMLElement>('button:not([disabled])')
    if (focusables.length === 0) {
      return
    }

    const first = focusables[0]!
    const last = focusables[focusables.length - 1]!
    const active = document.activeElement

    if (event.shiftKey && (active === first || active === panel)) {
      event.preventDefault()
      last.focus()
    } else if (!event.shiftKey && active === last) {
      event.preventDefault()
      first.focus()
    }
  }

  return (
    <>
      <div
        role="dialog"
        aria-modal="true"
        className="msg-menu"
        aria-label="Val för meddelandet"
        tabIndex={-1}
        ref={panelRef}
        onKeyDown={trapFocus}
      >
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
            onReply()
            onClose()
          }}
        >
          Svara
        </button>

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

        {canDelete && !confirmingDelete && (
          <button
            type="button"
            className="msg-menu__item msg-menu__item--danger"
            disabled={deleting}
            onClick={() => {
              setConfirmingDelete(true)
            }}
          >
            Ta bort
          </button>
        )}

        {canDelete && confirmingDelete && (
          <div role="group" aria-label="Bekräfta radering">
            <button
              type="button"
              className="msg-menu__item msg-menu__item--danger"
              disabled={deleting}
              autoFocus
              onClick={() => {
                onDelete()
                onClose()
              }}
            >
              Bekräfta radering
            </button>
            <button
              type="button"
              className="msg-menu__item"
              onClick={() => {
                setConfirmingDelete(false)
              }}
            >
              Avbryt
            </button>
          </div>
        )}
      </div>

      {/*
        Klick utanför stänger. Ligger efter panelen och är aria-hidden, så det aldrig blir en
        fokuserbar "Stäng menyn"-knapp före innehållet (#404). Tangentbordet stänger med Escape.
      */}
      <div className="msg-menu__backdrop" aria-hidden="true" onClick={onClose} />
    </>
  )
}
