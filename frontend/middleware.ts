import { next } from '@vercel/edge'

/*
 * Edge-skyddet (§KM.11, checklista 4.6, #584).
 *
 * Render-URL:en ar publikt nabar -- vercel.ts proxar /api dit. Utan det har kan en angripare som
 * gar forbi Vercel forfalska en ny X-Forwarded-For per anrop och fa en egen rate-limit-hink varje
 * gang (inkl. mot inloggningen). Darfor satter edge en delad hemlig header pa varje /api-request;
 * backend kraver den och avvisar allt annat med 403 (se EdgeAuthentication i backend).
 *
 * Mellanvaran ror INTE sjalva proxningen: den lagger bara till headern och kallar next(), sa att
 * vercel.ts-rewriten gor rewriten till Render precis som forr (forstaparts-cookien, X-Forwarded-*
 * -- allt orort). Minsta mojliga yta: bara /api traffas (matcher nedan).
 *
 * Hemligheten kommer ur Vercel-varen KARRA_EDGE_AUTH och far ALDRIG ligga i repot (§0 p.7). Ar den
 * osatt skickas ingen header och requesten lamnas orord -- backend-grinden ar da ocksa inert, sa
 * koden kan rullas ut innan hemligheten finns pa bade Vercel och Render (se utrullningsordningen i
 * EdgeAuthentication).
 */
export const config = {
  matcher: '/api/:path*',
}

export default function middleware(request: Request): Response {
  const secret = process.env.KARRA_EDGE_AUTH

  // Ingen hemlighet konfigurerad -> lamna requesten orord (grinden inert i bada andar).
  if (!secret) {
    return next()
  }

  const headers = new Headers(request.headers)
  headers.set('x-karra-edge-auth', secret)
  return next({ request: { headers } })
}
