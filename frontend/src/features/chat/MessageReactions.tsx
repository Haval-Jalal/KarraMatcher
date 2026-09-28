import { reactionLabel, type ChatChannel, type ChatMessage } from './chatApi'
import { useToggleReaction } from './useChat'

/**
 * Reaktions-chipsen under ett meddelande (`#301`, `#redesign`): de reaktioner som redan finns,
 * med antal och markering om jag själv reagerat. Att <em>lägga till</em> en reaktion sker numera
 * i meddelandets långtryck-meny ({@link MessageMenu}) — chipsen här togglar en befintlig.
 *
 * <para>
 * Reaktionen är ingen fritext — bara en av en fast uppsättning (servern är grinden). Varje chip är
 * en riktig knapp med svensk etikett och <c>aria-pressed</c>, så den fungerar för tangentbord och
 * skärmläsare (WCAG 2.1 AA). Färgen är aldrig enda signalen. Finns inga reaktioner ritas inget.
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

  // Servern skickar alltid en lista; `?? []` skyddar mot ett äldre/mockat svar utan fältet.
  const active = (message.reactions ?? []).filter((reaction) => reaction.count > 0)

  if (active.length === 0) {
    return null
  }

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
    </div>
  )
}
