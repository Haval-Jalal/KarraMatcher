import type { CSSProperties } from 'react'

import { contrastRatio } from './contrast'

/**
 * Lagfärgen som tema.
 *
 * Lagen har fyra färger med helt olika ljushet — Gul `#D9A21B` och Vit `#D9D9D9` är
 * ljusa, Blå `#1E3F8A` och Svart `#161616` är mörka. En fast textfärg ovanpå dem kan
 * därför aldrig fungera för alla fyra, vilket är anledningen till att lagfärgen tidigare
 * bara fick vara en ram och aldrig en yta bakom text.
 *
 * Det var att lösa fel problem. Räknas textfärgen fram per lagfärg klarar alla fyra
 * lagen god marginal till WCAG:s 4,5:1 — och det gäller även den femte färgen, den som
 * en tränare lägger in nästa säsong utan att fråga någon.
 *
 * Färgen räcker aldrig ensam som betydelsebärare (WCAG 1.4.1). Den säger vilket lag man
 * tittar på, inte något som inte också står i text.
 */

/**
 * Bläck att sätta på en lagfärgad yta.
 *
 * Rent svart, inte `--text-strong` (`#0d0d10`). Det spelar roll: mellantoner är det
 * svåra fallet, eftersom varken ljust eller mörkt bläck har mycket marginal där. Med
 * nästan-svart blir sämsta fallet **4,45:1** och faller alltså under AA — `#996699` är
 * ett exempel. Med rent svart är sämsta fallet **4,67:1** och det finns ingen färg som
 * inte håller.
 *
 * Skillnaden syns inte för ögat, men den är skillnaden mellan en regel som gäller för
 * alla färger och en som gäller för de fyra vi råkar ha i dag.
 */
const INK_DARK = '#000000'
const INK_LIGHT = '#ffffff'

/**
 * Textfärgen som ska ligga på `colorHex` — den av de två som ger högst kontrast.
 *
 * Att välja den *högsta* och inte den första som duger gör att marginalen blir så stor
 * som färgen tillåter. Det spelar roll på en telefonskärm i solsken vid en fotbollsplan.
 */
export function inkFor(colorHex: string): string {
  return contrastRatio(INK_DARK, colorHex) >= contrastRatio(INK_LIGHT, colorHex)
    ? INK_DARK
    : INK_LIGHT
}

/**
 * Variablerna som gör en vy till lagets. Sätts på vyns yttersta element, så att allt
 * inuti kan använda dem utan att veta vilket lag det gäller.
 */
export function teamThemeStyle(colorHex: string | undefined): CSSProperties | undefined {
  if (!colorHex) {
    return undefined
  }

  return {
    '--team-accent': colorHex,
    '--team-ink': inkFor(colorHex),
  } as CSSProperties
}
