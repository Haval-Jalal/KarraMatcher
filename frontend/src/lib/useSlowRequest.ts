import { useEffect, useState } from 'react'

/**
 * Sant när ett anrop har hållit på längre än det borde.
 *
 * Backend sover på Renders fria nivå: efter ungefär en kvarts tystnad stängs den av, och
 * tar omkring 50 sekunder att vakna. Appen används mest lördag morgon, efter en tyst natt
 * — alltså är den långsamma starten det vanligaste första intrycket, inte undantaget.
 *
 * §KM.11 kräver därför att ett långsamt anrop förklarar sig på svenska i stället för att
 * visa en snurra som ser trasig ut. Det här är mätningen som avgör när det ska ske.
 *
 * Tröskeln är satt så att ett normalt anrop aldrig hinner utlösa den. Ett svar från
 * Vercels edge kommer på tiotals millisekunder; en väckt backend tar sekunder.
 */
export const SLOW_AFTER_MS = 2500

export function useSlowRequest(isPending: boolean, delayMs: number = SLOW_AFTER_MS): boolean {
  const [isSlow, setIsSlow] = useState(false)
  const [trackedPending, setTrackedPending] = useState(isPending)

  /*
   * Nollställningen sker under render och inte i en effekt.
   *
   * Ett nytt anrop ska aldrig ärva förra anropets besked — annars påstår appen att
   * servern sover i samma stund som man laddar om. Att justera tillstånd under render är
   * Reacts eget mönster för just det: React kör om komponenten direkt, innan något ritas,
   * så mellanläget syns aldrig. En effekt hade ritat det gamla beskedet först och tagit
   * bort det efteråt, vilket är ett synligt blink.
   */
  if (trackedPending !== isPending) {
    setTrackedPending(isPending)
    setIsSlow(false)
  }

  useEffect(() => {
    if (!isPending) {
      return
    }

    const timer = setTimeout(() => {
      setIsSlow(true)
    }, delayMs)

    return () => {
      clearTimeout(timer)
    }
  }, [isPending, delayMs])

  return isSlow
}
