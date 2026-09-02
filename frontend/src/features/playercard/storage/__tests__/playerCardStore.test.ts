import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'

import { requestPersistentStorage } from '../persistentStorage'
import { clearCard, readCard, writeCard } from '../playerCardStore'
import { CURRENT_VERSION, emptyCard } from '../schema'

// Källfilerna läses som text för de kontroller som gäller kopplingar, inte beteende.
import STORE_SOURCE from '../playerCardStore.ts?raw'
import PERSIST_SOURCE from '../persistentStorage.ts?raw'
import SCHEMA_SOURCE from '../schema.ts?raw'

/**
 * Spelarkortets lagring (`#42`, §KM.2, checklistan 3.4 och 3.5).
 *
 * <h3>Varför testerna här är stränga</h3>
 *
 * Kortet finns bara på enheten. Det finns ingen kopia på servern att hämta tillbaka, så
 * ett fel i den här filen är inte ett fel som går att laga i efterhand — det är en
 * förlorad säsong hos varje familj som redan använt appen.
 */

beforeEach(() => {
  localStorage.clear()
})

afterEach(() => {
  vi.unstubAllGlobals()
})

describe('ingenting härifrån går ut på nätet', () => {
  it('importerar inte API-lagret', () => {
    /*
     * Kärnan i §KM.2. Vagen till ett natverksanrop finns inte i de har filerna, sa
     * barnets statistik kan inte av misstag skickas nagonstans.
     *
     * Kontrollen ar pa import och inte pa beteende med flit: ett beteendetest fangar bara
     * de anrop testet rakar utlosa, medan en import syns oavsett vem som anropar vad.
     */
    for (const source of [STORE_SOURCE, PERSIST_SOURCE, SCHEMA_SOURCE]) {
      expect(source).not.toContain('@/lib/api')
      expect(source).not.toContain('fetch(')
    }
  })

  it('rör inte nätet när kortet sparas eller läses', () => {
    const fetchMock = vi.fn()
    vi.stubGlobal('fetch', fetchMock)

    writeCard({ ...emptyCard(), children: [child()] })
    readCard()

    expect(fetchMock).not.toHaveBeenCalled()
  })
})

describe('datan överlever', () => {
  it('läses tillbaka som den skrevs', () => {
    const card = { ...emptyCard(), children: [child()], reports: [report()] }

    expect(writeCard(card)).toBe(true)

    const read = readCard()

    expect(read.children).toHaveLength(1)
    expect(read.reports[0]?.goals).toBe(2)
  })

  it('överlever en omladdning', () => {
    // localStorage lever kvar mellan sidladdningar; testet speglar det genom att läsa
    // med en ny anrop utan något i minnet.
    writeCard({ ...emptyCard(), children: [child()] })

    expect(readCard().children[0]?.name).toBe('Elias')
  })

  it('stämplar alltid nuvarande version', () => {
    writeCard({ ...emptyCard(), version: 0 })

    expect(readCard().version).toBe(CURRENT_VERSION)
  })
})

describe('appen går aldrig sönder av lagringen', () => {
  it('ger ett tomt kort när lagringen är tom', () => {
    expect(readCard()).toEqual(emptyCard())
  })

  it.each(['inte json alls', '{', '[]', 'null', '{"version":1}'])(
    'ger ett tomt kort för trasig data: %s',
    (raw) => {
      /*
       * En trasig blob far inte gora appen omojlig att oppna. Skulle lasningen kasta vore
       * aven den friska datan oatkomlig -- och en forlorad sasong ser likadan ut for en
       * foralder oavsett vad orsaken var.
       */
      localStorage.setItem('karra.spelarkort', raw)

      expect(readCard()).toEqual(emptyCard())
    },
  )

  it('säger ifrån när lagringen är full i stället för att låtsas', () => {
    // Den som fyllt i en matchrapport ska få veta att den inte sparades.
    vi.stubGlobal('localStorage', {
      getItem: () => null,
      setItem: () => {
        throw new Error('QuotaExceededError')
      },
      removeItem: () => undefined,
    })

    expect(writeCard(emptyCard())).toBe(false)
  })

  it('klarar att lagringen är helt avstängd', () => {
    vi.stubGlobal('localStorage', undefined)

    expect(readCard()).toEqual(emptyCard())
    expect(() => {
      clearCard()
    }).not.toThrow()
  })
})

describe('beständig lagring begärs när kortet får innehåll', () => {
  it('frågar inte för den som bara läser schemat', () => {
    /*
     * De allra flesta rör aldrig spelarkortet. Att fraga dem vore att visa en ruta for
     * nagot de inte gor -- och en fraga man inte forstar besvaras med nej, vilket gor
     * skyddet samre for dem som sedan borjar anvanda kortet.
     */
    const persist = vi.fn().mockResolvedValue(true)
    vi.stubGlobal('navigator', { storage: { persist, persisted: () => Promise.resolve(false) } })

    writeCard(emptyCard())

    expect(persist).not.toHaveBeenCalled()
  })
})

describe('beständig lagring', () => {
  it('ber om den', async () => {
    const persist = vi.fn().mockResolvedValue(true)
    vi.stubGlobal('navigator', { storage: { persist, persisted: () => Promise.resolve(false) } })

    expect(await requestPersistentStorage()).toBe('beviljad')
    expect(persist).toHaveBeenCalledOnce()
  })

  it('frågar inte igen när den redan är beviljad', async () => {
    // Vissa webbläsare visar en ruta för varje förfrågan.
    const persist = vi.fn()
    vi.stubGlobal('navigator', { storage: { persist, persisted: () => Promise.resolve(true) } })

    expect(await requestPersistentStorage()).toBe('beviljad')
    expect(persist).not.toHaveBeenCalled()
  })

  it('hanterar ett nej utan att gå sönder', async () => {
    // Appen fungerar likadant utan beständig lagring — den är bara mer utsatt.
    vi.stubGlobal('navigator', {
      storage: { persist: () => Promise.resolve(false), persisted: () => Promise.resolve(false) },
    })

    expect(await requestPersistentStorage()).toBe('nekad')
  })

  it('hanterar en webbläsare som saknar funktionen', async () => {
    // iOS har den knappt. Det är därför installation på hemskärmen är den riktiga
    // motåtgärden (§KM.2), inte den här begäran.
    vi.stubGlobal('navigator', {})

    expect(await requestPersistentStorage()).toBe('saknas')
  })
})

function child() {
  return { id: 'barn-1', name: 'Elias', shirtNumber: '7', teamSlug: 'gul' }
}

function report() {
  return {
    id: 'rapport-1',
    childId: 'barn-1',
    matchId: null,
    playedUtc: '2026-09-20T12:00:00.000Z',
    goals: 2,
    assists: 1,
    note: null,
  }
}
