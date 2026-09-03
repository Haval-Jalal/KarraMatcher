import { Link, useParams } from '@tanstack/react-router'

import { formatMatchDate } from '@/lib/time'
import { useDocumentTitle } from '@/lib/useDocumentTitle'

import { BadgeList } from '../badges/BadgeList'
import { readCard } from '../storage/playerCardStore'
import { possessive, seasonFor, summarise, type SeasonRow } from './season'

/**
 * Barnets egen sida.
 *
 * <h3>Ett samlarkort, inte ett kalkylark</h3>
 *
 * Totalerna står stort och först, säsongen sammanfattas i meningar, och märkena får plats
 * på samma sida som siffrorna de kommer ur. Matchlistan ligger sist: den är belägget, inte
 * poängen.
 *
 * <h3>Ingenting hämtas</h3>
 *
 * Allt på sidan räknas ur kortet på enheten (§KM.2). Den fungerar därför i flygplansläge,
 * på en telefon utan valt lag, och för en familj som just importerat sin säsong från den
 * gamla appen — och det finns ingen väg härifrån ut på nätet.
 */
export function PlayerCardPage() {
  const { childId } = useParams({ from: '/spelarkort/$childId' })
  const card = readCard()
  const child = card.children.find((candidate) => candidate.id === childId)

  useDocumentTitle(child?.name ?? 'Spelarkortet')

  if (child === undefined) {
    return (
      <main>
        <header className="app-header">
          <h1>Barnet finns inte här</h1>
        </header>

        <p className="state">
          Kortet ligger på telefonen, så en länk hit fungerar bara på den telefon barnet är tillagt
          på.
        </p>

        <Link className="button" to="/spelarkort">
          Till spelarkortet
        </Link>
      </main>
    )
  }

  const season = seasonFor(card, child.id)
  const sentences = summarise(season, child.name)

  return (
    <main>
      <header className="app-header">
        <h1>{child.name}</h1>

        {child.shirtNumber !== null && child.shirtNumber !== '' && (
          <p className="app-header__subtitle">{`Nummer ${child.shirtNumber}`}</p>
        )}
      </header>

      {season.totals.matches === 0 ? (
        <EmptySeason name={child.name} />
      ) : (
        <>
          <dl className="totals">
            <Total label="Matcher" value={season.totals.matches} />
            <Total label="Mål" value={season.totals.goals} />
            <Total label="Assist" value={season.totals.assists} />
            <Total label="Poäng" value={season.points} />
          </dl>

          <section>
            <h2>Säsongen</h2>

            {sentences.map((sentence) => (
              <p key={sentence} className="season__sentence">
                {sentence}
              </p>
            ))}
          </section>
        </>
      )}

      <section>
        <h2>Märken</h2>

        <BadgeList card={card} childId={child.id} />
      </section>

      {season.rows.length > 0 && (
        <section>
          <h2>Matcherna</h2>

          <ul className="season">
            {season.rows.map((row) => (
              <MatchRow key={row.id} row={row} />
            ))}
          </ul>
        </section>
      )}

      <Link className="button" to="/spelarkort">
        Tillbaka till spelarkortet
      </Link>
    </main>
  )
}

/**
 * Tomt läge.
 *
 * <para>
 * Ett barn utan matcher ska inte mötas av nollor. Nollor ser ut som ett omdöme; den här
 * texten säger i stället vad som händer härnäst och var det görs.
 * </para>
 */
function EmptySeason({ name }: { name: string }) {
  return (
    <section>
      <p className="state">
        {`Här kommer ${possessive(name)} matcher att synas. Öppna matchen i schemat efter att den spelats och fyll i tillsammans — mål, assist och hur det gick.`}
      </p>

      <p className="state">Första matchen är den roligaste att fylla i.</p>
    </section>
  )
}

function Total({ label, value }: { label: string; value: number }) {
  return (
    <div className="totals__item">
      <dt>{label}</dt>
      <dd>{value}</dd>
    </div>
  )
}

/**
 * En rad per match.
 *
 * <para>
 * Motståndaren står när rapporten minns den. Äldre rapporter, och matcher som importerats
 * från den gamla appen, bär bara datumet — då står datumet ensamt i stället för att vi
 * hittar på ett lagnamn.
 * </para>
 */
function MatchRow({ row }: { row: SeasonRow }) {
  const result =
    row.teamGoals === null || row.opponentGoals === null
      ? null
      : `${String(row.teamGoals)}–${String(row.opponentGoals)}`

  return (
    <li className="season__row">
      <span className="season__when">
        <b>{row.opponent ?? 'Match'}</b>
        <span className="season__date">{formatMatchDate(row.playedUtc)}</span>
      </span>

      {result !== null && (
        <span className="season__result" aria-label={`Resultat ${result}`}>
          {result}
        </span>
      )}

      <span className="season__effort">
        <Effort emoji="⚽" label="Mål" value={row.goals} />
        <Effort emoji="🎯" label="Assist" value={row.assists} />
      </span>
    </li>
  )
}

/**
 * Ett mått i matchraden.
 *
 * <para>
 * Emojin är dekor och därför dold för skärmläsaren — ordet bär innehållet. Den ska läsa
 * "2 mål", inte "fotboll 2".
 * </para>
 */
function Effort({ emoji, label, value }: { emoji: string; label: string; value: number }) {
  return (
    <span className="season__measure">
      <span aria-hidden="true">{emoji}</span>
      <span className="visually-hidden">{label}</span>
      <span>{value}</span>
    </span>
  )
}
