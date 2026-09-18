/**
 * Formen på ett schema som håller på att hämtas.
 *
 * Rutorna följer matchkortets egen layout — bred rad för tiden, smalare för motståndaren,
 * smalast för platsen. Poängen är att sidan inte ska hoppa när innehållet landar: det som
 * står här står på samma ställe som det som kommer.
 *
 * Antalet är tre. Fler hade lovat mer än vi vet, och laget kan mycket väl ha en enda
 * match kvar.
 */
function CardShape() {
  return (
    <div className="skeleton-card">
      <span className="skeleton skeleton--time" />
      <span className="skeleton skeleton--opponent" />
      <span className="skeleton skeleton--venue" />
    </div>
  )
}

export function ScheduleSkeleton() {
  return (
    <div className="skeleton-list">
      <CardShape />
      <CardShape />
      <CardShape />
    </div>
  )
}
