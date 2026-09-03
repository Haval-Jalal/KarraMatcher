import { unseenBadges } from './badges'
import type { Child, PlayerCardData } from '../storage/schema'

/**
 * Den lilla festen när ett märke låses upp.
 *
 * <h3>Varför den ligger i matchrapporten</h3>
 *
 * Märket låses upp i det ögonblick målet fylls i. Att i stället visa det nästa gång någon
 * öppnar spelarkortet hade gjort firandet till en notis om något som hände förut — och
 * det är just kopplingen till stunden som gör att ett barn vill fylla i nästa match.
 *
 * <h3>En gång, inte varje gång</h3>
 *
 * Det firade märket sparas som sett på barnet. Utan det hade samma sex märken firats vid
 * varje omstart, och en händelse blivit en påminnelse man klickar bort.
 *
 * <h3>Rörelsen är grädden, aldrig beskedet</h3>
 *
 * Texten säger allt: vem, vilket märke och varför. Animationen är inlindad i
 * `prefers-reduced-motion: no-preference` och tillför ingenting den som stängt av rörelse
 * går miste om (§KM.0 A3).
 */
export function BadgeCelebration({
  card,
  children,
  onAcknowledge,
}: {
  card: PlayerCardData
  children: Child[]
  onAcknowledge: () => void
}) {
  const news = children.flatMap((child) =>
    unseenBadges(card, child.id).map((badge) => ({ child, badge })),
  )

  if (news.length === 0) {
    return null
  }

  return (
    <div className="celebration" role="status">
      <h3 className="celebration__title">
        {news.length === 1 ? 'Nytt märke!' : `${String(news.length)} nya märken!`}
      </h3>

      <ul className="celebration__list">
        {news.map(({ child, badge }) => (
          <li key={`${child.id}-${badge.id}`} className="celebration__item">
            <span className="celebration__emoji" aria-hidden="true">
              {badge.emoji}
            </span>
            <span>
              <b>{child.name}</b>
              {` låste upp ${badge.name} — ${badge.requirement.toLowerCase()}.`}
            </span>
          </li>
        ))}
      </ul>

      <button type="button" className="button" onClick={onAcknowledge}>
        Så bra!
      </button>
    </div>
  )
}
