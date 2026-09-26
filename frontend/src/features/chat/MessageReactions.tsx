import { useState } from 'react'

import { REACTION_EMOJIS, reactionLabel, type ChatChannel, type ChatMessage } from './chatApi'
import { useToggleReaction } from './useChat'

/**
 * Reaktionsraden under ett meddelande (`#301`): de reaktioner som redan finns visas som chips
 * (antal + markerad om jag själv reagerat), och en "+"-knapp fäller ut de tillåtna emojierna.
 *
 * <para>
 * Reaktionen är ingen fritext — bara en av en fast uppsättning (servern är grinden). Varje
 * kontroll är en riktig knapp med svensk etikett och <c>aria-pressed</c>, så den fungerar för
 * tangentbord och skärmläsare (WCAG 2.1 AA). Färgen är aldrig enda signalen.
 * </para>
 */
export function MessageReactions({
  message,
  channel,
}: {
  message: ChatMessage
  channel: ChatChannel
}) {
  const toggle = useToggleReaction(channel)
  const [pickerOpen, setPickerOpen] = useState(false)

  // Servern skickar alltid en lista; `?? []` skyddar mot ett äldre/mockat svar utan fältet.
  const active = (message.reactions ?? []).filter((reaction) => reaction.count > 0)

  return (
    <div className="reactions">
      {active.map((reaction) => {
        const name = reactionLabel[reaction.emoji] ?? reaction.emoji

        return (
          <button
            key={reaction.emoji}
            type="button"
            className={reaction.mine ? 'reaction reaction--mine' : 'reaction'}
            aria-pressed={reaction.mine}
            aria-label={`${name}, ${reaction.count}`}
            disabled={toggle.isPending}
            onClick={() => toggle.mutate({ messageId: message.id, emoji: reaction.emoji })}
          >
            <span aria-hidden="true">{reaction.emoji}</span>
            <span className="reaction__count">{reaction.count}</span>
          </button>
        )
      })}

      <button
        type="button"
        className="reaction reaction--add"
        aria-label="Lägg till en reaktion"
        aria-expanded={pickerOpen}
        onClick={() => setPickerOpen((open) => !open)}
      >
        <span aria-hidden="true">+</span>
      </button>

      {pickerOpen && (
        <div className="reactions__picker" role="group" aria-label="Välj reaktion">
          {REACTION_EMOJIS.map((emoji) => (
            <button
              key={emoji}
              type="button"
              className="reaction"
              aria-label={reactionLabel[emoji] ?? emoji}
              disabled={toggle.isPending}
              onClick={() => {
                toggle.mutate({ messageId: message.id, emoji })
                setPickerOpen(false)
              }}
            >
              <span aria-hidden="true">{emoji}</span>
            </button>
          ))}
        </div>
      )}
    </div>
  )
}
