# SÄKERHETS-CHECKLISTA — auditerbar (Kärra Matcher: webb-PWA + .NET-backend)

> **Syfte:** Konkret, avbockbar lista som visar att appen uppfyller en hög säkerhetsnivå.
> Mappad mot **OWASP ASVS**, **OWASP API Security Top 10** och **OWASP Top 10 (webb)**,
> samt projektets skärpta regler i [`CLAUDE.md`](./CLAUDE.md) §KM.
> Används som **grind före varje produktionsrelease**.
>
> **Ursprung:** anpassad från projektmallen `StrukturTelefonApp`, utanför repot. Mobilspecifika
> sektioner (MASVS-STORAGE, MASVS-PLATFORM, app store-krav) är ersatta med motsvarigheter för webb och PWA.
>
> **Status per rad:** `✅ klar` · `🟡 pågår` · `⬜ ej börjad` · `➖ ej tillämpligt (motivera)`.
> Rader märkta *(vid behov)* bockas av när triggern i [`STANDARDER-VID-BEHOV.md`](./STANDARDER-VID-BEHOV.md) uppfyllts.
> Rader märkta **§KM** kommer från projektets egna regler och väger tyngst.

---

## 1. Autentisering & sessioner
| # | Kontroll | Status |
|---|----------|:------:|
| 1.1 | Inloggning via e-postkod — inga lösenord alls, alltså inget att läcka | ✅ |
| 1.2 | Engångskoder är tidsbegränsade (10 min), engångsanvända och kryptografiskt slumpade; lagras hashade | ✅ |
| 1.3 | JWT validerar issuer, audience, lifetime och signing key | ✅ |
| 1.4 | Access-token kortlivad (15 min); refresh-token i `httpOnly`-cookie med `Secure` + `SameSite=Lax` | ✅ |
| 1.5 | Refresh tokens roteras + återanvändning detekteras (hela familjen ogiltigförklaras) | ✅ |
| 1.6 | Token-revocation vid utloggning; kontoradering kaskaderar bort tokens (raderingsflödet självt i `#33`) | 🟡 |
| 1.7 | Spärr efter 5 felgissningar per kod, plus egen rate limit på inloggningens endpoints | ✅ |
| 1.8 | Ingen sessionsfixering — varje inloggning startar en ny token-familj | ✅ |
| 1.9 | MFA *(vid behov — ej aktuellt för denna app)* | ➖ |

## 2. Auktorisering (åtkomstkontroll)
| # | Kontroll | Status |
|---|----------|:------:|
| 2.1 | Policy-baserad auktorisering — inga hårdkodade rollkontroller i controllers | ✅ |
| 2.2 | **Objektnivå-auktorisering på varje resurs (mot IDOR)** | ⬜ |
| 2.3 | **§KM.2** Ingen endpoint tar emot eller returnerar spelarstatistik — verifierat med arkitekturtest | ✅ |
| 2.4 | **§KM.2** Ingen entitet, tabell eller migration för barnstatistik finns i backend | ✅ |
| 2.5 | Resurs som tillhör annan användare svarar `404`, inte `403` | ⬜ |
| 2.6 | Tränarroll är bunden till **sitt lag** — kan inte ändra andra lags matcher | ✅ |
| 2.7 | **§KM.7** `AttendanceEnabled` kontrolleras serverside; avstängd flagga ger `404` | ⬜ |
| 2.8 | **§KM.12** Endast erbjudandets ägare kan acceptera eller neka dess förfrågningar | ⬜ |
| 2.9 | **§KM.12** Platsräkning sker server-side; accept som spränger antalet avvisas | ⬜ |
| 2.10 | **§KM.12** Nekande utan meddelande avvisas server-side | ⬜ |
| 2.11 | **§KM.3** Gäst kan läsa samåkning men får `401` på att lägga upp eller skicka förfrågan | ⬜ |
| 2.12 | Ingen "mass assignment" — DTOs (records), aldrig entiteter, i API-in/ut | ✅ |
| 2.13 | Principen om minsta behörighet genomgående | ⬜ |

## 3. Datalagring i klienten (webb / PWA)
| # | Kontroll | Status |
|---|----------|:------:|
| 3.1 | JWT ligger **aldrig** i `localStorage` eller `sessionStorage` — access-token i minnet | ⬜ |
| 3.2 | Refresh-token endast i `httpOnly`-cookie, oåtkomlig för JavaScript | ⬜ |
| 3.3 | **§KM.8** Service worker cachar aldrig auth-svar | ✅ |
| 3.3b | **§KM.8** Schemat är läsbart offline — verifierat i flygplansläge på iPhone och Android | ✅ |
| 3.4 | **§KM.2** Spelarkortet lagras i enhetens egen lagring och skickas aldrig i något anrop | ⬜ |
| 3.5 | **§KM.2** `navigator.storage.persist()` begärs; nekad begäran hanteras utan att appen går sönder | ⬜ |
| 3.6 | **§KM.2** Säkerhetskopieringskod finns, uppmanas till, och kan återställas på annan enhet | ⬜ |
| 3.7 | **§KM.2** Användaren informeras tydligt om att spelarkortet bor i telefonen | ⬜ |
| 3.8 | Inga hemligheter i frontend-bundeln — verifierat i byggd output | ⬜ |
| 3.9 | Inga secrets, tokens eller PII i loggar eller felrapportering (scrubbat) | ⬜ |
| 3.10 | Cache-headers hindrar mellanliggande cachning av inloggade svar (`Cache-Control: private, no-store`) | ✅ |

## 4. Nätverk & transport
| # | Kontroll | Status |
|---|----------|:------:|
| 4.1 | All trafik över HTTPS — inga klartext-anrop, HTTP redirectas | ⬜ |
| 4.2 | HSTS påtvingat med tillräcklig max-age | ⬜ |
| 4.3 | CORS låst till kända origins — aldrig `AllowAnyOrigin` i prod | ⬜ |
| 4.4 | **§KM.11** Klienten anropar bara `/api/*` via Vercel-rewriten — Render-URL:en finns inte i frontend-koden | ✅ |
| 4.5 | **§KM.11** Refresh-cookien är förstapart: `HttpOnly`, `Secure`, `SameSite=Lax` | ⬜ |
| 4.6 | **§KM.11** Backend på Render tar inte emot trafik som kringgår proxyn med annan origin | ⬜ |
| 4.6b | Rate limiting partitioneras på klientens IP från `X-Forwarded-For`, med en opartitionerad skyddsgräns som inte går att kringgå genom att förfalska adressen | ⬜ |
| 4.7 | **§KM.3** Publika endpoints returnerar aldrig personuppgifter | ✅ |
| 4.8 | SSRF-skydd: väder-API anropas med koordinater från egen databas, aldrig från användarindata | ✅ |
| 4.9 | Web Push använder egna VAPID-nycklar; payload innehåller ingen PII | ⬜ |

## 5. Webbplattform & klientintegritet
| # | Kontroll | Status |
|---|----------|:------:|
| 5.1 | **CSP** satt och restriktiv — inga `unsafe-inline`/`unsafe-eval` i prod | ⬜ |
| 5.2 | `X-Content-Type-Options: nosniff`, `X-Frame-Options: DENY`, `Referrer-Policy` satta | ⬜ |
| 5.3 | Ingen `dangerouslySetInnerHTML` på otillförlitlig data | ⬜ |
| 5.4 | Service worker registreras bara över HTTPS och har begränsat scope | ✅ |
| 5.5 | Uppdatering av service worker hanteras — användaren fastnar inte på gammal version | ✅ |
| 5.6 | Utgående länkar (kartor) använder `rel="noopener noreferrer"` | ✅ |
| 5.7 | ICS-feeden är publik men innehåller **enbart** matchdata (§KM.4) | ✅ |
| 5.8 | Öppen omdirigering omöjlig — inga redirect-mål från query-parametrar | ⬜ |

## 6. Indata, kod & vanliga sårbarheter
| # | Kontroll | Status |
|---|----------|:------:|
| 6.1 | All input valideras server-side (FluentValidation); klientvalidering är endast UX | ⬜ |
| 6.2 | Parametriserade queries / EF Core → ingen SQL-injection | ⬜ |
| 6.3 | Massinläggs-parsern hanterar skadlig och trasig indata utan att krascha eller injicera | ⬜ |
| 6.4 | Rate limiting på publika endpoints och inloggning; `429` med `Retry-After` (**§KM.0 A1**) | ✅ |
| 6.5 | CSRF-skydd aktivt (anti-forgery + `SameSite`) eftersom refresh-token ligger i cookie | ✅ |
| 6.6 | Inga interna fel eller stack traces läcker till klient (ProblemDetails) | ⬜ |
| 6.7 | Filuppladdning saknas — eller, om den införs, typ-, storleks- och innehållsvalideras | ➖ |

## 7. Secrets & konfiguration
| # | Kontroll | Status |
|---|----------|:------:|
| 7.1 | Secrets i secret store / user-secrets — aldrig i repo | ⬜ |
| 7.2 | `.env` och nycklar aldrig incheckade (blockerat av hook) | ⬜ |
| 7.3 | Separat konfiguration per miljö (dev / staging / prod) | ⬜ |
| 7.4 | `.env.example` incheckad med alla variabelnamn utan värden | ⬜ |
| 7.5 | VAPID-privatnyckel och e-postleverantörens nyckel enbart i backend | ⬜ |
| 7.6 | Typad config via Options-mönstret med `ValidateOnStart()` | ⬜ |
| 7.7 | Den gamla JSONBin-master-nyckeln är **roterad och ogiltigförklarad** | ⬜ |
| 7.8 | Secrets-rotation *(vid behov)* | ⬜ |

## 8. Loggning, audit & övervakning
| # | Kontroll | Status |
|---|----------|:------:|
| 8.1 | Strukturerad loggning (Serilog) med correlation-ID per request | ⬜ |
| 8.2 | **§KM.10** Aldrig barnnamn, e-post, push-endpoint, JWT eller användarfritext i loggar | ⬜ |
| 8.3 | **§KM.10** Audit-logg för: matchändring, inställd match, rolländring, trupp­ändring, radera konto | ⬜ |
| 8.4 | Audit-loggen är oföränderlig och innehåller vem, vad, när och correlation-ID | ⬜ |
| 8.5 | Larm eller uppföljning vid upprepade inloggningsfel och behörighetsavslag | ⬜ |
| 8.6 | Felrapportering aktiv och PII-scrubbad | ⬜ |

## 9. Barn, integritet & GDPR
> **Projektets känsligaste sektion.** Appen behandlar uppgifter om barn under 13 år.

| # | Kontroll | Status |
|---|----------|:------:|
| 9.0 | **§KM.2** Vid lansering, med kallelsen avstängd, lagrar servern **inga uppgifter alls om barn** | ⬜ |
| 9.1 | **§KM.1** Om truppen aktiveras: endast förnamn och tröjnummer — verifierat mot databasschemat | ⬜ |
| 9.2 | **§KM.1** Inga efternamn, personnummer, födelsedatum, adresser, foton eller positioner någonstans | ⬜ |
| 9.3 | **§KM.1** Ny PII-kolumn har beslut infört i `docs/PROJEKT-HANDOFF.md` | ⬜ |
| 9.4 | **§KM.6** Vårdnadshavarsamtycke inhämtas innan barn kopplas; version och tidsstämpel sparas | ⬜ |
| 9.5 | **§KM.6** Radering av barn tar bort spelare, rapporter, närvarosvar och koppling — direkt, ej mjuk | ⬜ |
| 9.6 | **§KM.6** Radering av konto tar bort kontot och allt det äger | ⬜ |
| 9.7 | Laglig grund dokumenterad i `SPEC.md` per uppgiftstyp | ⬜ |
| 9.8 | Gallringsregler implementerade: push-prenumerationer, **samåkning 30 dagar efter match**, gamla säsonger | ⬜ |
| 9.9 | Dataminimering — varje fält kan motiveras med en funktion som kräver det | ⬜ |
| 9.10 | **§KM.6** Ingen besöksanalys, ingen spårning, inga tredjepartsskript utöver väder och kartlänkar | ⬜ |
| 9.11 | Synlig och begriplig integritetstext i appen, skriven för föräldrar — inte jurister | ⬜ |
| 9.12 | Data lagras och behandlas inom EU — **med ett beslutat undantag: e-postleverantören**, se 9.14 | ⬜ |
| 9.13 | Registerutdrag går att lämna ut på begäran (export av en familjs data) | ⬜ |
| 9.14 | **Undantaget till 9.12 är dokumenterat:** biträdesavtal tecknat med Resend, överföringsgrunden (DPF eller SCC) kontrollerad och namngiven i integritetstexten, och gallringstid för leverantörens loggar kontrollerad | ⬜ |

## 10. Beroenden & leveranskedja
| # | Kontroll | Status |
|---|----------|:------:|
| 10.1 | `dotnet list package --vulnerable` rent i CI | ⬜ |
| 10.2 | `npm audit` utan kända allvarliga sårbarheter | ⬜ |
| 10.3 | Automatiska uppdaterings-PR:ar (Dependabot eller Renovate) aktiva | ⬜ |
| 10.4 | .NET och Node på versioner som fortfarande får säkerhetsuppdateringar | ⬜ |
| 10.5 | Låsfiler (`package-lock.json`, `packages.lock.json`) incheckade | ⬜ |

## 11. Drift & återhämtning
| # | Kontroll | Status |
|---|----------|:------:|
| 11.1 | Health checks `/health` och `/health/ready` svarar korrekt | ⬜ |
| 11.2 | **§KM.0 A2** DB-backup automatisk **och återställning testad minst en gång** | ✅ |
| 11.3 | Migrations körs kontrollerat vid deploy; rollback-plan finns | ⬜ |
| 11.4 | Planerat underhåll läggs aldrig fredag–söndag under säsong | ⬜ |
| 11.5 | Maintenance-läge visar en begriplig svensk text, inte ett serverfel | ⬜ |
| 11.6 | **§KM.11** Databasen ligger på Neon — inte på en gratisnivå som upphör efter 30 dagar | ⬜ |
| 11.7 | **§KM.11** Kallstart maskerad: publika GET-svar besvaras av Vercels edge utan att väcka backend | ✅ |
| 11.8 | **§KM.11** Uppetidsverktyg pingar `/health` och larmar när backend inte svarar | ✅ |
| 11.9 | Docker-containern kör som **non-root** och exponerar bara port 8080 | ⬜ |

## 12. Testning & verifiering (före release)
| # | Kontroll | Status |
|---|----------|:------:|
| 12.1 | Säkerhetsrelaterade enhetstester (auktorisering, validering) gröna | ⬜ |
| 12.2 | Arkitekturtester skyddar lagergränserna och hindrar entiteter i controllersignaturer | ✅ |
| 12.3 | **§KM.2** Arkitekturtest bevisar att ingen spelarstatistik-endpoint existerar | ✅ |
| 12.4 | **§KM.5** Tidszonstest över sommartidsskiftet i oktober är grönt | ✅ |
| 12.5 | E2E-test av de fem kritiska flödena i `SPEC.md` §9 | ⬜ |
| 12.6 | A11y-genomgång: tangentbord, skärmläsare, kontrast, fokus (WCAG 2.1 AA) | ✅ |
| 12.7 | SAST/DAST kört inför lansering; fynd åtgärdade | ⬜ |
| 12.8 | Testad på riktig iPhone och riktig Android — inte bara i desktop-emulering | ⬜ |
| 12.9 | Penetrationstest *(vid behov)* | ⬜ |

---

> **Releasegrind:** Alla baslinjerader ska vara `✅` — eller `➖` med skriven motivering — innan en
> produktionsrelease. Rader märkta **§KM** får aldrig sättas till `➖`.
> Uppdatera statusen i **samma PR** som åtgärden.
