import { useState } from 'react'

import { ApiError, postJson } from '@/lib/api'

/** Ett utfall per rad, som servern beskriver det. */
type LineOutcome =
  'Ok' | 'Skipped' | 'Incomplete' | 'BadDateOrTime' | 'UnknownReference' | 'Duplicate' | 'OtherTeam'

interface ParsedLine {
  lineNumber: number
  rawText: string
  outcome: LineOutcome
  problem: string | null
}

interface ImportResult {
  lines: ParsedLine[]
  imported: number
}

/**
 * Vad varje utfall betyder för tränaren.
 *
 * Texterna säger vad som händer med raden, inte vad parsern tyckte. "Ofullständig" är ett
 * omdöme; "Hoppas över — saknar uppgifter" är ett besked.
 */
const OUTCOME_TEXT: Record<LineOutcome, string> = {
  Ok: 'Läggs till',
  Skipped: 'Hoppas över — tom rad eller rubrik',
  Incomplete: 'Hoppas över — saknar uppgifter',
  BadDateOrTime: 'Hoppas över — datum eller tid går inte att tolka',
  UnknownReference: 'Hoppas över — okänt lag eller spelplats',
  Duplicate: 'Hoppas över — matchen finns redan',
  OtherTeam: 'Hoppas över — gäller ett annat lag',
}

/**
 * Massinlägg av ett helt schema.
 *
 * <h3>Ingen ska behöva lita på en parser i blindo</h3>
 *
 * Tränaren klistrar in, ser rad för rad vad som blir av, och sparar först efter det.
 * Granskningen är det som gör funktionen trygg nog att användas — och det som gör att
 * någon vågar klistra in tjugofem rader i stället för att knappa in dem.
 *
 * <h3>Delvis import är avsiktlig</h3>
 *
 * En trasig rad hindrar inte de tjugofyra som är rätt. Tränaren ser vilka som hoppades
 * över och rättar dem för hand.
 */
export function ScheduleImport({ slug, onImported }: { slug: string; onImported: () => void }) {
  const [text, setText] = useState('')
  const [result, setResult] = useState<ImportResult | null>(null)
  const [failure, setFailure] = useState<string | null>(null)
  const [working, setWorking] = useState(false)

  const ready = result?.lines.filter((line) => line.outcome === 'Ok').length ?? 0

  const run = async (path: string) => {
    setWorking(true)
    setFailure(null)

    try {
      return await postJson<ImportResult>(
        `/api/v1/teams/${encodeURIComponent(slug)}/matches/${path}`,
        { text },
      )
    } catch (error) {
      setFailure(
        error instanceof ApiError && error.offline
          ? 'Ingen anslutning. Kontrollera nätet och försök igen.'
          : 'Det gick inte just nu. Försök igen om en stund.',
      )

      return null
    } finally {
      setWorking(false)
    }
  }

  return (
    <section className="import">
      <h2>Klistra in hela schemat</h2>

      <p className="state">
        En rad per match: datum, tid, lag, motståndare och spelplats. Tabb, semikolon eller komma
        mellan fälten — ett kopierat kalkylark fungerar som det är.
      </p>

      <div className="form__field">
        <label htmlFor="schema">Inklistrat schema</label>
        <textarea
          id="schema"
          rows={8}
          value={text}
          onChange={(event) => {
            setText(event.target.value)
            setResult(null)
          }}
        />
      </div>

      {failure !== null && (
        <p className="state state--error" role="alert">
          {failure}
        </p>
      )}

      <div className="actions">
        <button
          type="button"
          className="button"
          disabled={working || text.trim() === ''}
          onClick={() => {
            void (async () => {
              const preview = await run('import/preview')

              if (preview !== null) {
                setResult(preview)
              }
            })()
          }}
        >
          {working ? 'Granskar…' : 'Granska'}
        </button>

        {result !== null && ready > 0 && (
          <button
            type="button"
            className="button"
            disabled={working}
            onClick={() => {
              void (async () => {
                const imported = await run('import')

                if (imported !== null) {
                  setResult(imported)
                  setText('')
                  onImported()
                }
              })()
            }}
          >
            {`Lägg till ${String(ready)} ${ready === 1 ? 'match' : 'matcher'}`}
          </button>
        )}
      </div>

      {result !== null && (
        <>
          {result.imported > 0 && (
            <p className="state" role="status">
              {`${String(result.imported)} matcher tillagda. Föräldrarnas kalendrar uppdateras.`}
            </p>
          )}

          <div className="scroll">
            <table className="import__table">
              <caption>Rad för rad</caption>
              <thead>
                <tr>
                  <th scope="col">Rad</th>
                  <th scope="col">Innehåll</th>
                  <th scope="col">Vad som händer</th>
                </tr>
              </thead>
              <tbody>
                {result.lines.map((line) => (
                  <tr key={line.lineNumber}>
                    <td>{line.lineNumber}</td>
                    <td className="import__raw">{line.rawText}</td>
                    <td>
                      {/* Ordet bär beskedet, inte färgen (WCAG 1.4.1). */}
                      {OUTCOME_TEXT[line.outcome]}
                      {line.problem !== null && ` — ${line.problem}`}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </>
      )}
    </section>
  )
}
