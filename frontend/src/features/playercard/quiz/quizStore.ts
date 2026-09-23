/**
 * Sportquizets rekord på enheten (`#206`).
 *
 * <h3>Skilt från spelarkortet med flit</h3>
 *
 * Rekordet är en trivial kul-siffra, inte statistik. Det ligger därför i en <b>egen</b>
 * lagringsnyckel och inte i spelarkortets blob — det ska inte tvinga fram en migrering av
 * kortets känsliga schema, och det hör inte hemma i säkerhetskopian (§KM.2). Precis som
 * kortet lämnar det aldrig telefonen: modulen känner inte till API-lagret.
 *
 * <h3>Aldrig ett undantag</h3>
 *
 * Lagringen kan vara avstängd (privat läge, policy). En quiz-siffra får aldrig hindra
 * sidan från att öppnas, så varje läsning och skrivning är inlindad — en miss ger bara
 * inget rekord, inte ett fel.
 */

const STORAGE_KEY = 'karra.sportquiz'

interface QuizRecord {
  bestScore: number
}

/** Läser rekordet. Svarar med 0 när inget finns eller lagringen inte går att läsa. */
export function readBestScore(): number {
  let raw: string | null

  try {
    raw = globalThis.localStorage?.getItem(STORAGE_KEY) ?? null
  } catch {
    return 0
  }

  if (raw === null) {
    return 0
  }

  try {
    const parsed: unknown = JSON.parse(raw)

    if (
      parsed !== null &&
      typeof parsed === 'object' &&
      typeof (parsed as Partial<QuizRecord>).bestScore === 'number'
    ) {
      const best = (parsed as QuizRecord).bestScore

      return Number.isFinite(best) && best > 0 ? Math.floor(best) : 0
    }

    return 0
  } catch {
    return 0
  }
}

/**
 * Sparar ett resultat om det slår rekordet. Svarar med det gällande rekordet efteråt, så
 * att den som just spelat får se rätt siffra utan en ny läsning.
 */
export function recordScore(score: number): number {
  const best = readBestScore()

  if (score <= best) {
    return best
  }

  try {
    globalThis.localStorage?.setItem(STORAGE_KEY, JSON.stringify({ bestScore: score }))

    return score
  } catch {
    // Gick det inte att spara står det gamla rekordet kvar; siffran är inte värd ett fel.
    return best
  }
}
