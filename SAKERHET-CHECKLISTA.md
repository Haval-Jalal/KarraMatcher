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
>
> **Reviderad `#69` (2026-09-15):** hela listan gick igenom rad för rad, verifierad mot koden.
> Kvarvarande icke-gröna rader är antingen **externa åtgärder** (kräver dig, inte kod) eller
> **beroende av senare issues** (`#70` SAST/DAST, `#71` E2E, `#72` enhetstester). De är märkta i klartext.

---

## 1. Autentisering & sessioner
| # | Kontroll | Status |
|---|----------|:------:|
| 1.1 | Inloggning via e-postkod — inga lösenord alls, alltså inget att läcka | ✅ |
| 1.2 | Engångskoder är tidsbegränsade (10 min), engångsanvända och kryptografiskt slumpade; lagras hashade | ✅ |
| 1.3 | JWT validerar issuer, audience, lifetime och signing key | ✅ |
| 1.4 | Access-token kortlivad (15 min); refresh-token i `httpOnly`-cookie med `Secure` + `SameSite=Lax` | ✅ |
| 1.5 | Refresh tokens roteras + återanvändning detekteras (hela familjen ogiltigförklaras) | ✅ |
| 1.6 | Token-revocation vid utloggning och vid kontoradering | ✅ |
| 1.7 | Spärr efter 5 felgissningar per kod, plus egen rate limit på inloggningens endpoints | ✅ |
| 1.8 | Ingen sessionsfixering — varje inloggning startar en ny token-familj | ✅ |
| 1.9 | MFA *(vid behov — ej aktuellt för denna app)* | ➖ |

## 2. Auktorisering (åtkomstkontroll)
| # | Kontroll | Status |
|---|----------|:------:|
| 2.1 | Policy-baserad auktorisering — inga hårdkodade rollkontroller i controllers | ✅ |
| 2.2 | **Objektnivå-auktorisering på varje resurs (mot IDOR)** — ägarkontroll i varje handler, täckt av tester | ✅ |
| 2.3 | **§KM.2** Ingen endpoint tar emot eller returnerar spelarstatistik — verifierat med arkitekturtest | ✅ |
| 2.4 | **§KM.2** Ingen entitet, tabell, kolumn eller migration för barnstatistik finns i backend — verifierat med arkitekturtest | ✅ |
| 2.5 | Resurs som tillhör annan användare svarar `404`, inte `403` | ✅ |
| 2.6 | Tränarroll är bunden till **sitt lag** — kan inte ändra andra lags matcher | ✅ |
| 2.7 | **§KM.7** `AttendanceEnabled` kontrolleras serverside; avstängd flagga ger `404` | ✅ |
| 2.8 | **§KM.12** Endast erbjudandets ägare kan acceptera eller neka dess förfrågningar | ✅ |
| 2.9 | **§KM.12** Platsräkning sker server-side; accept som spränger antalet avvisas | ✅ |
| 2.10 | **§KM.12** Nekande utan meddelande avvisas server-side | ✅ |
| 2.11 | **§KM.3** Gäst kan läsa samåkning men får `401` på att lägga upp eller skicka förfrågan | ✅ |
| 2.12 | Ingen "mass assignment" — DTOs (records), aldrig entiteter, i API-in/ut | ✅ |
| 2.13 | Principen om minsta behörighet genomgående (policy-baserat, ingen fallback-policy, gäst-läs/inloggad-skriv) | ✅ |

## 3. Datalagring i klienten (webb / PWA)
| # | Kontroll | Status |
|---|----------|:------:|
| 3.1 | JWT ligger **aldrig** i `localStorage` eller `sessionStorage` — access-token i minnet | ✅ |
| 3.2 | Refresh-token endast i `httpOnly`-cookie, oåtkomlig för JavaScript | ✅ |
| 3.3 | **§KM.8** Service worker cachar aldrig auth-svar | ✅ |
| 3.3b | **§KM.8** Schemat är läsbart offline — verifierat i flygplansläge på iPhone och Android | ✅ |
| 3.4 | **§KM.2** Spelarkortet lagras i enhetens egen lagring och skickas aldrig i något anrop | ✅ |
| 3.5 | **§KM.2** `navigator.storage.persist()` begärs; nekad begäran hanteras utan att appen går sönder | ✅ |
| 3.6 | **§KM.2** Säkerhetskopieringskod finns, uppmanas till, och kan återställas på annan enhet | ✅ |
| 3.7 | **§KM.2** Användaren informeras tydligt om att spelarkortet bor i telefonen | ✅ |
| 3.8 | Inga hemligheter i frontend-bundeln — verifierat: bara `VITE_API_BASE_URL` (icke-hemlig) används | ✅ |
| 3.9 | Inga secrets, tokens eller PII i loggar eller felrapportering (scrubbat) — alla loggrader id-baserade (§KM.10) | ✅ |
| 3.10 | Cache-headers hindrar mellanliggande cachning av inloggade svar (`Cache-Control: private, no-store`) | ✅ |

## 4. Nätverk & transport
| # | Kontroll | Status |
|---|----------|:------:|
| 4.1 | All trafik över HTTPS — inga klartext-anrop, HTTP redirectas *(TLS + HTTP→HTTPS vid Vercels och Renders edge)* | ✅ |
| 4.2 | HSTS påtvingat med tillräcklig max-age *(SecurityHeadersMiddleware + `vercel.json`, `#69`)* | ✅ |
| 4.3 | CORS låst till kända origins — aldrig `AllowAnyOrigin` i prod *(ingen CORS-policy alls: en origin via rewrite, §KM.11)* | ✅ |
| 4.4 | **§KM.11** Klienten anropar bara `/api/*` via Vercel-rewriten — Render-URL:en finns inte i frontend-koden | ✅ |
| 4.5 | **§KM.11** Refresh-cookien är förstapart: `HttpOnly`, `Secure`, `SameSite=Lax` | ✅ |
| 4.6 | **§KM.11** Backend på Render tar inte emot trafik som kringgår proxyn med annan origin — **⚠️ EXTERN: kräver att direktåtkomst till Render-URL:en stängs i drift** | 🟡 |
| 4.6b | Rate limiting partitioneras på klientens IP från `X-Forwarded-For`, med en opartitionerad skyddsgräns som inte går att kringgå genom att förfalska adressen | ✅ |
| 4.7 | **§KM.3** Publika endpoints returnerar aldrig personuppgifter | ✅ |
| 4.8 | SSRF-skydd: väder-API anropas med koordinater från egen databas, aldrig från användarindata | ✅ |
| 4.9 | Web Push använder egna VAPID-nycklar; payload innehåller ingen PII | ✅ |

## 5. Webbplattform & klientintegritet
| # | Kontroll | Status |
|---|----------|:------:|
| 5.1 | **CSP** satt och restriktiv *(`vercel.json` + API-middleware, `#69`)*. `script-src 'self'` utan inline; **`style-src` kräver `'unsafe-inline'` för dynamiska inline-stilar (lagfärg, märkesstaplar) — verifiera i Vercel-preview innan lansering** | 🟡 |
| 5.2 | `X-Content-Type-Options: nosniff`, `X-Frame-Options: DENY`, `Referrer-Policy` satta *(middleware + `vercel.json`, `#69`)* | ✅ |
| 5.3 | Ingen `dangerouslySetInnerHTML` på otillförlitlig data — ingen förekomst i koden | ✅ |
| 5.4 | Service worker registreras bara över HTTPS och har begränsat scope | ✅ |
| 5.5 | Uppdatering av service worker hanteras — användaren fastnar inte på gammal version | ✅ |
| 5.6 | Utgående länkar (kartor) använder `rel="noopener noreferrer"` | ✅ |
| 5.7 | ICS-feeden är publik men innehåller **enbart** matchdata (§KM.4) | ✅ |
| 5.8 | Öppen omdirigering omöjlig — inga redirect-mål från query-parametrar (`next` valideras: bara interna vägar) | ✅ |

## 6. Indata, kod & vanliga sårbarheter
| # | Kontroll | Status |
|---|----------|:------:|
| 6.1 | All input valideras server-side (FluentValidation); klientvalidering är endast UX | ✅ |
| 6.2 | Parametriserade queries / EF Core → ingen SQL-injection (ingen rå SQL) | ✅ |
| 6.3 | Massinläggs-parsern hanterar skadlig och trasig indata utan att krascha eller injicera | ✅ |
| 6.4 | Rate limiting på publika endpoints och inloggning; `429` med `Retry-After` (**§KM.0 A1**) | ✅ |
| 6.5 | CSRF-skydd aktivt (anti-forgery + `SameSite`) eftersom refresh-token ligger i cookie | ✅ |
| 6.6 | Inga interna fel eller stack traces läcker till klient (ProblemDetails) | ✅ |
| 6.7 | Filuppladdning saknas — eller, om den införs, typ-, storleks- och innehållsvalideras | ➖ |

## 7. Secrets & konfiguration
| # | Kontroll | Status |
|---|----------|:------:|
| 7.1 | Secrets i secret store / user-secrets — aldrig i repo | ✅ |
| 7.2 | `.env` och nycklar aldrig incheckade — `.gitignore` blockerar `.env*`/`*.secrets*`/`vapid*.json`; global hook blockerar läsning; inga incheckade | ✅ |
| 7.3 | Separat konfiguration per miljö — `ASPNETCORE_ENVIRONMENT` + Render-env; bas-`appsettings.json` incheckad, per-miljö-filer gitignorerade | ✅ |
| 7.4 | `.env.example` incheckad med alla variabelnamn utan värden (backend + frontend) | ✅ |
| 7.5 | VAPID-privatnyckel och e-postleverantörens nyckel enbart i backend — ingen `VITE_`-nyckel | ✅ |
| 7.6 | Typad config via Options-mönstret med `ValidateOnStart()` (Auth, Push, EdgeCache) | ✅ |
| 7.7 | Den gamla JSONBin-master-nyckeln är **roterad och ogiltigförklarad** — **⚠️ EXTERN: måste roteras på jsonbin.io av en människa (nyckeln finns inte i repot)** | ⬜ |
| 7.8 | Secrets-rotation *(vid behov — manuell procedur dokumenterad i `backend/.env.example`)* | ➖ |

## 8. Loggning, audit & övervakning
| # | Kontroll | Status |
|---|----------|:------:|
| 8.1 | Strukturerad loggning (Serilog) med correlation-ID per request | ✅ |
| 8.2 | **§KM.10** Aldrig barnnamn, e-post, push-endpoint, JWT eller användarfritext i loggar | ✅ |
| 8.3 | **§KM.10** Audit-logg för: matchändring ✅, inställd match ✅, radera konto ✅ — rolländring och truppändring kvar (M3) | 🟡 |
| 8.4 | Audit-loggen är oföränderlig och innehåller vem, vad, när — oföränderlig + vem/vad/när klart; **correlation-ID i audit-raden återstår** | 🟡 |
| 8.5 | Larm eller uppföljning vid upprepade inloggningsfel och behörighetsavslag — **ej byggt (planeras efter lansering)** | ⬜ |
| 8.6 | Felrapportering aktiv och PII-scrubbad — **ingen extern felrapportering (bara Serilog-konsol); planeras efter lansering** | ⬜ |

## 9. Barn, integritet & GDPR
> **Projektets känsligaste sektion.** Appen behandlar uppgifter om barn under 13 år.

| # | Kontroll | Status |
|---|----------|:------:|
| 9.0 | **§KM.2** Vid lansering, med kallelsen avstängd, lagrar servern **inga uppgifter alls om barn** — verifierat med arkitekturtest | ✅ |
| 9.1 | **§KM.1** Om truppen aktiveras: endast förnamn och tröjnummer — **uppfyllt genom frånvaro: ingen trupp/barn-tabell finns (beslut 2026-09-10); återöppnas om trupp införs** | ✅ |
| 9.2 | **§KM.1** Inga efternamn, personnummer, födelsedatum, adresser, foton eller positioner någonstans — verifierat mot schemat (kontot lagrar bara e-post + valfritt namn) | ✅ |
| 9.3 | **§KM.1** Ny PII-kolumn har beslut infört i `docs/PROJEKT-HANDOFF.md` — processen finns och är använd | ✅ |
| 9.4 | **§KM.6** Vårdnadshavarsamtycke innan barn kopplas — **uppfyllt genom frånvaro: ingen barnkoppling finns på servern idag; införs trupp krävs samtycke på riktigt + skrivet beslut** | ✅ |
| 9.5 | **§KM.6** Radering av barn tar bort spelare, rapporter, närvarosvar och koppling — **ingen barn-entitet på servern; spelarkortet raderas på enheten av familjen** | ✅ |
| 9.6 | **§KM.6** Radering av konto tar bort kontot och allt det äger | ✅ |
| 9.7 | Laglig grund dokumenterad i `SPEC.md` per uppgiftstyp *(konsoliderad rad under §10, ej per-fält-tabell)* | ✅ |
| 9.8 | Gallringsregler implementerade: push-prenumerationer (döda reaktivt + tysta 12 mån), **samåkning 30 dagar efter match**. *Gamla säsonger behålls medvetet — matchdata är inte PII och kalenderfeeden beror på den (beslut i handoff, `#68`).* | ✅ |
| 9.9 | Dataminimering — varje fält kan motiveras med en funktion som kräver det | ✅ |
| 9.10 | **§KM.6** Ingen besöksanalys, ingen spårning, inga tredjepartsskript utöver väder och kartlänkar | ✅ |
| 9.11 | Synlig och begriplig integritetstext i appen, skriven för föräldrar — inte jurister (`#66`) | ✅ |
| 9.12 | Data lagras och behandlas inom EU — Render Frankfurt + Neon; **⚠️ EXTERN: bekräfta faktiska regioner i Neon/Render-dashboards** (undantag: e-postleverantören, se 9.14) | 🟡 |
| 9.13 | Registerutdrag går att lämna ut på begäran (export av en familjs data) (`#67`) | ✅ |
| 9.14 | **Undantaget till 9.12 är dokumenterat:** överföringsgrunden (DPF) namngiven i integritetstexten ✅; **⚠️ EXTERN: biträdesavtal med Resend + kontroll av leverantörens loggtid (juridisk/ops-åtgärd)** | 🟡 |

## 10. Beroenden & leveranskedja
| # | Kontroll | Status |
|---|----------|:------:|
| 10.1 | `dotnet list package --vulnerable` rent i CI *(tillagt `#69`; rent lokalt)* | ✅ |
| 10.2 | `npm audit` utan kända allvarliga sårbarheter *(tillagt i CI `#69`; 0 sårbarheter)* | ✅ |
| 10.3 | Automatiska uppdaterings-PR:ar (Dependabot) aktiva *(`.github/dependabot.yml`, `#69`)* | ✅ |
| 10.4 | .NET och Node på versioner som fortfarande får säkerhetsuppdateringar (.NET 10, Node 22) | ✅ |
| 10.5 | Låsfiler incheckade — `package-lock.json` ✅; **backend `packages.lock.json` saknas (NuGet-lockfiler ej aktiverade)** | 🟡 |

## 11. Drift & återhämtning
| # | Kontroll | Status |
|---|----------|:------:|
| 11.1 | Health checks `/health` och `/health/ready` svarar korrekt (readiness inkl. DB) | ✅ |
| 11.2 | **§KM.0 A2** DB-backup automatisk **och återställning testad minst en gång** | ✅ |
| 11.3 | Migrations körs kontrollerat vid deploy; rollback-plan finns (logisk dump före varje migration) | ✅ |
| 11.4 | Planerat underhåll läggs aldrig fredag–söndag under säsong *(dokumenterad policy i `SPEC.md`, ej teknisk grind)* | ✅ |
| 11.5 | Maintenance-läge visar en begriplig svensk text, inte ett serverfel — **svensk kallstartstext finns; ingen egen maintenance-sida** | 🟡 |
| 11.6 | **§KM.11** Databasen ligger på Neon — inte på en gratisnivå som upphör efter 30 dagar | ✅ |
| 11.7 | **§KM.11** Kallstart maskerad: publika GET-svar besvaras av Vercels edge utan att väcka backend | ✅ |
| 11.8 | **§KM.11** Uppetidsverktyg pingar `/health` och larmar när backend inte svarar | ✅ |
| 11.9 | Docker-containern kör som **non-root** och exponerar bara port 8080 (`USER $APP_UID`, `EXPOSE 8080`) | ✅ |

## 12. Testning & verifiering (före release)
| # | Kontroll | Status |
|---|----------|:------:|
| 12.1 | Säkerhetsrelaterade enhetstester (auktorisering, validering) gröna | ✅ |
| 12.2 | Arkitekturtester skyddar lagergränserna och hindrar entiteter i controllersignaturer | ✅ |
| 12.3 | **§KM.2** Arkitekturtest bevisar att ingen spelarstatistik-endpoint existerar | ✅ |
| 12.4 | **§KM.5** Tidszonstest över sommartidsskiftet i oktober är grönt | ✅ |
| 12.5 | E2E-test av de fem kritiska flödena i `SPEC.md` §9 — **beroende av `#71`** | ⬜ |
| 12.6 | A11y-genomgång: tangentbord, skärmläsare, kontrast, fokus (WCAG 2.1 AA) | ✅ |
| 12.7 | SAST/DAST kört inför lansering; fynd åtgärdade — **SAST (CodeQL) kör i CI ✅** (`#70`); **⚠️ DAST-workflow (ZAP, manuell) finns — körning mot staging + fyndhantering kräver deployad miljö** | 🟡 |
| 12.8 | Testad på riktig iPhone och riktig Android — inte bara i desktop-emulering — **beroende av `#72`** | ⬜ |
| 12.9 | Penetrationstest *(vid behov)* | ➖ |

---

> **Releasegrind:** Alla baslinjerader ska vara `✅` — eller `➖` med skriven motivering — innan en
> produktionsrelease. Rader märkta **§KM** får aldrig sättas till `➖`.
> Uppdatera statusen i **samma PR** som åtgärden.
>
> **Kvar före lansering (`#69`-revision, 2026-09-15):**
> - **Externa åtgärder (du):** 7.7 rotera JSONBin-nyckeln · 9.14 biträdesavtal med Resend · 9.12 bekräfta Neon/Render-regioner · 4.6 stäng direktåtkomst till Render-URL:en.
> - **Beroende av senare issues:** 12.5 (`#71` E2E) · 12.8 (`#72` enhetstester).
> - **Verifiering:** 5.1 bekräfta CSP i en Vercel-preview · 12.7 kör DAST-workflowen (ZAP) mot staging och bedöm fynden · granska CodeQL-fynd under fliken Security.
> - **Mindre kvar:** 8.4 correlation-ID i audit-raden · 8.5/8.6 larm & felrapportering (efter lansering) · 10.5 NuGet-lockfiler · 11.5 egen maintenance-sida.
