import { describe, expect, it } from 'vitest'

import FONTS_CSS from '@/styles/fonts.css?raw'
import INDEX_CSS from '@/styles/index.css?raw'

// Ingen alias för repotoppen — index.html ligger utanför `src/`.
import INDEX_HTML from '../../../index.html?raw'

/**
 * Typsnitten är den sortens sak som går sönder tyst.
 *
 * Skrivs en sökväg fel faller webbläsaren tillbaka på systemtypsnittet utan att säga
 * något — sidan ser ut att fungera, och ingen upptäcker det förrän någon jämför med hur
 * det var tänkt. Det var precis det som hände innan `#115`: `--sans` sa `Barlow`, men
 * filen fanns inte, och appen renderade i system-ui i flera veckor utan att något
 * varnade.
 */

/** Filerna som faktiskt ligger i `public/fonts`, enligt vad Vite hittar på disk. */
const onDisk = new Set(
  Object.keys(import.meta.glob('../../../public/fonts/*.woff2')).map(
    (path) => path.split('/').pop() as string,
  ),
)

/** Filerna `fonts.css` säger att den vill ha. */
const referenced = [...FONTS_CSS.matchAll(/url\('\/fonts\/([^']+)'\)/g)].map((m) => m[1] as string)

describe('varje deklarerat snitt finns på disk', () => {
  it('hittar minst de fyra snitten vi räknar med', () => {
    // Barlow 400 och 600, Barlow Condensed 600 och 700 — i två delmängder vardera.
    expect(referenced).toHaveLength(8)
  })

  it.each([...new Set(referenced)])('%s finns i public/fonts', (file) => {
    expect(onDisk.has(file)).toBe(true)
  })

  it('lämnar inga oanvända filer kvar', () => {
    // En vikt som slutar användas ska tas bort, inte ligga kvar och hämtas i onödan.
    // Barlow 500 låg med i första utkastet och fångades av just den här kontrollen.
    const unused = [...onDisk].filter((file) => !referenced.includes(file))

    expect(unused).toEqual([])
  })
})

describe('inget typsnitt hämtas utifrån', () => {
  // §KM.6 tillåter inga externa skript i frontenden utöver Open-Meteo och kartlänkarna.
  // En extern typsnittsvärd vore en ny tredjepart, och kräver ett skrivet beslut först.
  //
  // Ett externt snitt kan bara ta sig in genom att ett värdnamn skrivs ut någonstans i
  // källan — i en länk, ett @import eller en url(). Därför räcker det att läsa källan;
  // vi behöver inte bygga först.
  const sources: [string, string][] = [
    ['index.html', INDEX_HTML],
    ['fonts.css', FONTS_CSS],
    ['index.css', INDEX_CSS],
  ]

  const hosts = ['fonts.googleapis.com', 'fonts.gstatic.com', 'use.typekit.net', 'fontawesome']

  it.each(sources)('%s nämner ingen extern typsnittsvärd', (_name, source) => {
    for (const host of hosts) {
      expect(source.toLowerCase()).not.toContain(host)
    }
  })

  it.each(sources)('%s har inget @import mot en annan origin', (_name, source) => {
    expect(source).not.toMatch(/@import\s+(url\()?['"]?https?:/i)
  })
})

describe('snitten deklareras så att texten syns direkt', () => {
  it('använder font-display: swap överallt', () => {
    const faces = FONTS_CSS.match(/@font-face/g) ?? []
    const swaps = FONTS_CSS.match(/font-display:\s*swap/g) ?? []

    // Utan swap står texten osynlig medan snittet hämtas. På dålig täckning vid en
    // fotbollsplan är en tom sida värre än fel typsnitt i en halv sekund.
    expect(swaps).toHaveLength(faces.length)
  })

  it('begränsar latin-ext med unicode-range', () => {
    // Utan unicode-range hämtas båda delmängderna alltid. Svenska å, ä och ö ligger i
    // latin, så latin-ext ska bara kosta något när den faktiskt behövs.
    const faces = FONTS_CSS.match(/@font-face/g) ?? []
    const ranges = FONTS_CSS.match(/unicode-range:/g) ?? []

    expect(ranges).toHaveLength(faces.length)
  })

  it('förladdar de två snitt som syns först', () => {
    // Brödtexten och sidrubriken. Övriga vikter hinner hämtas medan sidan läses.
    expect(INDEX_HTML).toContain('/fonts/barlow-400-latin.woff2')
    expect(INDEX_HTML).toContain('/fonts/barlow-condensed-700-latin.woff2')
    expect(INDEX_HTML).toMatch(/rel="preload"/)
  })

  it('ger båda snitten en fallback som inte är serif', () => {
    // Under den halvsekund snittet hämtas är det fallbacken som syns. Ett oavsiktligt
    // serif-fallback gör att sidan hoppar i utseende när snittet landar.
    expect(INDEX_CSS).toMatch(/--sans:\s*'Barlow',[^;]*sans-serif;/)
    expect(INDEX_CSS).toMatch(/--display:\s*'Barlow Condensed',[^;]*sans-serif;/)
  })
})
