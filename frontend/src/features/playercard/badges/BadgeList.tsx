import { BADGES, isEarned, totalsFor } from './badges'
import type { PlayerCardData } from '../storage/schema'

/**
 * Alla märken för ett barn — upplåsta och låsta.
 *
 * <h3>Låsta märken visas, de göms inte</h3>
 *
 * Det är de låsta som gör listan värd att öppna. Ett barn som ser att det fattas två assist
 * till Passningskung har fått något att sikta på; ett barn som bara ser tre upplåsta märken
 * vet inte att det finns fler.
 *
 * <h3>Låst syns utan att bero på färg</h3>
 *
 * Föregångaren dämpade låsta märken med `opacity: .4` och inget annat. Det räcker inte här
 * (§KM.0 A3): dels faller kontrasten under 4,5:1, dels bärs beskedet då av ljushet ensam
 * (WCAG 1.4.1). Låsta märken har därför <b>full läsbar text</b>, ordet "Låst" för
 * skärmläsaren, och en mätare som säger hur långt barnet kommit.
 */
export function BadgeList({ card, childId }: { card: PlayerCardData; childId: string }) {
  const totals = totalsFor(card, childId)

  return (
    <ul className="badges">
      {BADGES.map((badge) => {
        const earned = isEarned(badge, totals)
        const { done, needed } = badge.progress(totals)

        return (
          <li key={badge.id} className={earned ? 'badge badge--earned' : 'badge'}>
            <span className="badge__emoji" aria-hidden="true">
              {badge.emoji}
            </span>

            <span className="badge__text">
              {/*
                Namnet star tva ganger med flit: en gang for ogat och en gang, med sitt
                lasta-eller-upplasta tillstand, som en enda mening for skarmlasaren. Delas
                den meningen i tva noder laser vissa skarmlasare upp dem var for sig, och
                "last" hamnar losryckt fran vilket marke det gallde. Samma monster som
                knapparna i barnlistan.
              */}
              <b className="badge__name">
                <span aria-hidden="true">{badge.name}</span>
                <span className="visually-hidden">
                  {`${badge.name} — ${earned ? 'upplåst' : 'låst'}`}
                </span>
              </b>

              <span className="badge__requirement">{badge.requirement}</span>

              {!earned && (
                <span className="badge__progress">
                  {/*
                   * Mataren visar samma sak som texten, inte nagot utover den. Den som inte
                   * ser den missar ingenting -- darfor aria-hidden pa sjalva stapeln.
                   */}
                  <span className="badge__bar" aria-hidden="true">
                    <span
                      className="badge__fill"
                      style={{ width: `${String(Math.round((done / needed) * 100))}%` }}
                    />
                  </span>
                  <span className="badge__count">{`${String(Math.min(done, needed))} av ${String(needed)}`}</span>
                </span>
              )}
            </span>
          </li>
        )
      })}
    </ul>
  )
}
