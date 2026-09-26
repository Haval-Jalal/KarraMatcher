// @ts-check
import { gzipSync } from 'node:zlib'
import { readFileSync, readdirSync } from 'node:fs'
import { join, dirname } from 'node:path'
import { fileURLToPath } from 'node:url'

/**
 * Prestandabudget-grind (§KM.11).
 *
 * Appen används mest lördag morgon på ett mobilnät vid en fotbollsplan med dålig täckning,
 * och backend sover på Renders fria nivå — den första sidladdningen är alltså det som avgör
 * intrycket. En budget som fäller bygget när den initiala JS-nyttolasten växer skyddar just
 * den laddningen: en tung feature får inte smyga in i entry-chunken utan att någon märker det.
 *
 * Vi mäter **gzip-storlek**, för det är det som faktiskt går över nätet. Tre tak:
 *
 *   - `entry`   — chunken index.html laddar direkt (det som blockerar första paint).
 *   - `totalJs` — all JS tillsammans (route-chunkar laddas vid behov, men summan sätter en
 *                 övre gräns för hela appen).
 *   - `css`     — hela stilmallen.
 *
 * Taken ligger med marginal över dagens siffror så ett normalt PR aldrig fälls av brus, men
 * en fördubbling eller en oavsiktligt ihopslagen chunk fångas. Sänk taken när bygget krymper
 * — de ska följa med nedåt, annars är budgeten snart bara dekoration.
 */

const BUDGETS_KB = {
  entry: 125,
  totalJs: 235,
  css: 10,
}

const here = dirname(fileURLToPath(import.meta.url))
const distDir = join(here, '..', 'dist')
const assetsDir = join(distDir, 'assets')

/** Gzip-storlek i kilobyte (samma enhet Vite rapporterar i). */
function gzipKb(path) {
  return gzipSync(readFileSync(path)).length / 1000
}

/** Entry-chunken är den type="module"-script index.html faktiskt laddar. */
function findEntryJs() {
  const html = readFileSync(join(distDir, 'index.html'), 'utf8')
  const match = html.match(/<script[^>]+type="module"[^>]+src="([^"]+\.js)"/)
  if (!match) {
    throw new Error('Hittade ingen entry-script i dist/index.html — har bygget körts?')
  }
  return join(distDir, match[1].replace(/^\//, ''))
}

const files = readdirSync(assetsDir)
const jsFiles = files.filter((f) => f.endsWith('.js')).map((f) => join(assetsDir, f))
const cssFiles = files.filter((f) => f.endsWith('.css')).map((f) => join(assetsDir, f))

const measured = {
  entry: gzipKb(findEntryJs()),
  totalJs: jsFiles.reduce((sum, f) => sum + gzipKb(f), 0),
  css: cssFiles.reduce((sum, f) => sum + gzipKb(f), 0),
}

const rows = Object.keys(BUDGETS_KB).map((key) => {
  const actual = measured[key]
  const budget = BUDGETS_KB[key]
  return {
    Post: key,
    'Gzip (kB)': actual.toFixed(1),
    'Budget (kB)': budget.toFixed(1),
    Marginal: `${(((budget - actual) / budget) * 100).toFixed(0)}%`,
    Status: actual <= budget ? 'OK' : 'ÖVER',
  }
})

console.table(rows)

const over = rows.filter((r) => r.Status === 'ÖVER')
if (over.length > 0) {
  console.error(
    '\nPrestandabudgeten överskriden (§KM.11):\n' +
      over
        .map((r) => `  ${r.Post}: ${r['Gzip (kB)']} kB > ${r['Budget (kB)']} kB budget`)
        .join('\n') +
      '\n\nAntingen: split ut det som växte till en egen route-chunk, eller — om ökningen är\n' +
      'befogad och bestående — höj taket i scripts/check-bundle-size.mjs medvetet.',
  )
  process.exit(1)
}

console.log('\nPrestandabudget: alla poster under taket.')
