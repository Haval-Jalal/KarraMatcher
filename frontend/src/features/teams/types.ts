/** Ett lag så som API:t levererar det. Speglar `TeamDto` i backend. */
export interface Team {
  slug: string
  name: string
  ageGroup: string
  /** Lagfärgen som hex, t.ex. `#D9A21B`. Driver appens accentfärg. */
  colorHex: string
}
