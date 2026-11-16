/**
 * Ber webbläsaren att inte gallra bort lagringen.
 *
 * <h3>Varför det här behövs</h3>
 *
 * Spelarkortet finns bara på enheten (§KM.2). En webbläsare som rensar lagringen för en
 * plats som inte använts på ett tag tar då med sig en hel säsong — och appen används
 * säsongsvis, alltså med långa uppehåll.
 *
 * <h3>Varför det inte räcker</h3>
 *
 * Begäran kan nekas, och på iOS finns den knappt. Det som faktiskt skyddar är
 * installation på hemskärmen, som `#48` uppmanar till, och säkerhetskopian i `#47`. Den
 * här funktionen är det billigaste av de tre, inte det starkaste.
 *
 * <b>Ett nej får aldrig gå ut över något.</b> Appen fungerar likadant utan beständig
 * lagring — den är bara mer utsatt.
 */
/**
 * Läser av om lagringen redan är beständig — <b>utan att fråga</b> (inget webbläsar-prompt).
 *
 * Används av gränssnittet för att stärka varningen när datan inte är skyddad: `requestPersistent-
 * Storage` ber om beständighet men dess svar kastas (begäran är poängen, inte returen), så den
 * här läser i stället det faktiska läget så att <c>StorageNotice</c> kan säga det rakt ut (`#614`).
 */
export async function isStoragePersisted(): Promise<'beständig' | 'ej-beständig' | 'saknas'> {
  const storage = globalThis.navigator?.storage as
    { persisted?: () => Promise<boolean> } | undefined

  if (storage?.persisted === undefined) {
    return 'saknas'
  }

  try {
    return (await storage.persisted()) ? 'beständig' : 'ej-beständig'
  } catch {
    return 'saknas'
  }
}

export async function requestPersistentStorage(): Promise<'beviljad' | 'nekad' | 'saknas'> {
  const storage = globalThis.navigator?.storage as
    { persisted?: () => Promise<boolean>; persist?: () => Promise<boolean> } | undefined

  if (storage?.persist === undefined) {
    return 'saknas'
  }

  try {
    // Redan beviljad? Då ska vi inte fråga igen — vissa webbläsare visar en ruta.
    if (storage.persisted !== undefined && (await storage.persisted())) {
      return 'beviljad'
    }

    return (await storage.persist()) ? 'beviljad' : 'nekad'
  } catch {
    return 'saknas'
  }
}
