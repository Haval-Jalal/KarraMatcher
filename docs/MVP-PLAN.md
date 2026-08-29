# MVP-PLAN — Kärra Matcher

> **Syfte:** Avgöra om ett issue hör till MVP-kärnan eller post-MVP (§0 p.12 i [`CLAUDE.md`](../CLAUDE.md)).
> Varje punkt nedan blir ett eller flera issues på boarden. Ordningen är avsiktlig — senare milstolpar
> förutsätter tidigare.
>
> **v1 = M0 till och med M8.** Allt byggs klart före lansering (beslut 2026-08-29), men varje milstolpe
> driftsätts löpande så den kan provköras på riktigt.

---

## M0 — Grund

*Ingen produktfunktion. Målet är att nästa milstolpe kan byggas utan friktion.*

- [ ] Installera .NET LTS-SDK *(maskinen har 9.0.309 — STS, utanför stöd sedan maj 2026)*
- [ ] `git init`, GitHub-repo, `.gitignore`, branch protection på `main`
- [ ] Konton: Vercel, Render, Neon (fria nivåer) + gratis uppetidsverktyg mot `/health`
- [ ] GitHub Projects-board: `Backlog → Ready → In Progress → In Review → Done`
- [ ] .NET-lösning: `Domain`, `Application`, `Infrastructure`, `Api` + fyra testprojekt
- [ ] Vite-frontend: TS strict, feature-mappar, path alias `@/`, TanStack Router och Query
- [ ] `.editorconfig`, ESLint, Prettier, `dotnet format`, `.env.example`
- [ ] CI: build, test och lint på varje push — bygget faller vid fel
- [ ] PostgreSQL uppsatt; EF Core konfigurerat; första migrationen
- [ ] Idempotent seed: 4 lag, 7 spelplatser med koordinater, 25 matcher från nuvarande schema
- [ ] Serilog, correlation-ID, ProblemDetails, health checks, rate limiting
- [ ] Arkitekturtester (NetArchTest) som skyddar lagergränserna
- [ ] Dockerfile: multi-stage, non-root, `ASPNETCORE_URLS=http://+:8080`, HEALTHCHECK *(mönstret från CarCheck)*
- [ ] `frontend/vercel.json` med `/api/:path*`-rewrite till Render och SPA-fallback
- [ ] Edge-cache-headers på publika GET-endpoints så kallstart inte drabbar vanliga sidvisningar (§KM.11)
- [ ] **DB-backup uppsatt och återställning testad en gång** (§KM.0 A2)
- [ ] Deploy: frontend på Vercel, backend på Render, databas på Neon — allt nås på riktiga URL:er

**Klar när:** ett tomt men driftsatt system svarar på `/health/ready` via Vercel-domänen (alltså genom
rewriten, inte direkt mot Render), har schemat i databasen, och CI är grön.

---

## M1 — Den publika delen

*Efter denna milstolpe är appen redan användbar för föräldrar — utan konto.*

- [ ] `GET /api/v1/teams` och `GET /api/v1/teams/{slug}/matches` — anonymt, utan PII
- [ ] `GET /api/v1/matches/{id}` — anonymt
- [ ] Output-cache och ETag på publika endpoints
- [ ] Lagväljare som minns valet, med lagfärg som tema
- [ ] Matchlista: kommande, idag, tidigare (hopfällbart), månadsrubriker
- [ ] "Nästa match"-kort med relativ dag
- [ ] Matchdetalj: datum, tid, plats, adress, hemma/borta, inställd
- [ ] Väder från Open-Meteo för matcher inom 15 dagar
- [ ] Vägbeskrivning — Apple Maps på iOS, Google Maps annars
- [ ] Enskild kalenderfil (`.ics`) per match
- [ ] **ICS-prenumeration per lag** med korrekt `SEQUENCE` och `STATUS:CANCELLED` (§KM.4)
- [ ] PWA: manifest, ikoner, service worker, offline-läsbart schema (§KM.8)
- [ ] Tidszonshantering med testfall över sommartidsskiftet (§KM.5)
- [ ] A11y-genomgång av den publika delen

**Klar när:** en förälder kan öppna länken, se sitt lags matcher, få vägbeskrivning och prenumerera på kalendern — utan konto, och med schemat läsbart offline.

---

## M2 — Konto och roller

*Auth-infrastrukturen. Krävs av tränaradmin och samåkning — men inte av spelarkortet.*

- [ ] Inloggning med e-postkod; koden är tidsbegränsad, engångsanvänd och slumpad
- [ ] Access-token i minnet, refresh-token i `httpOnly`-cookie med `Secure` och `SameSite=Lax`
- [ ] Refresh-token-rotation med återanvändnings-detektering
- [ ] Account lockout och rate limiting på inloggning
- [ ] CSRF-skydd
- [ ] Policy-baserad auktorisering: `Coach` (per lag), `Admin`
- [ ] Radera konto — direkt och fullständigt (§KM.6)
- [ ] Gäst kan fortsatt allt i M1 utan konto — verifierat med test

**Klar när:** en tränare kan logga in och ingen icke-tränare kommer åt en skyddad endpoint, samtidigt som hela den publika delen fungerar precis som förut utan konto.

---

## M3 — Tränaradmin

*Blockerad av öppna frågor #1 och #2 — förankra med tränarna först.*

- [ ] Skapa, ändra, ta bort och ställa in match — med audit-logg
- [ ] Tränarroll bunden till eget lag; `Admin` för alla lag
- [ ] Spelplatsregister med autocomplete
- [ ] **Massinlägg:** klistra in → tolka → förhandsgranska med status per rad → spara
- [ ] Parsern klarar tabb, komma, semikolon och blandade datumformat
- [ ] Dubblettdetektering mot befintliga matcher
- [ ] Truppvy: lägg till spelare med förnamn och tröjnummer (förberedelse för M6)
- [ ] Tränarens matchöversikt över hela säsongen

**Klar när:** en tränare kan lägga in hela säsongens schema på under fem minuter, ändra en match och ställa in en match — och ändringen syns direkt för föräldrarna.

---

## M4 — Spelarkortet (helt på enheten)

*Ingen backend alls. Kan byggas parallellt med M2 och M3, och är oberoende av öppna frågor.*

- [ ] Lägg till barn lokalt: namn eller smeknamn, valfritt tröjnummer, lag
- [ ] Fyll i per match: resultat, mål, assist, spelade — **inget nätverksanrop**
- [ ] Märken låses upp med en liten fest när de nås
- [ ] Spelarkort: totaler, upplåsta och låsta märken, en rad per spelad match
- [ ] Säsongssammanfattning per barn
- [ ] **Säkerhetskopieringskod** som kan kopieras och klistras in på en annan enhet
- [ ] Synlig uppmaning att spara koden, och en påminnelse när kortet har innehåll men aldrig kopierats
- [ ] Import av gammal `KARRA1.`-kod
- [ ] `navigator.storage.persist()` begärs; hantera att den nekas
- [ ] Installationstips på iOS — Safari kan annars rensa lagringen
- [ ] Ärlig text om var datan finns och att den försvinner utan säkerhetskopia
- [ ] Radera barn och radera allt — lokalt, direkt
- [ ] **Arkitekturtest:** ingen endpoint tar emot eller returnerar spelarstatistik (§KM.2)

**Klar när:** en förälder kan sitta med sitt barn efter matchen och fylla i, se ett märke låsas upp, och flytta allt till en ny telefon med en kod — utan konto och utan att en byte lämnar enheten.

---

## M5 — Samåkning

- [ ] Lägg upp erbjudande: riktning, avgångsplats, avgångstid, **1–4 platser**, valfri notis
- [ ] Gäst ser erbjudandena; knappar leder till inloggning i stället för tyst fel (§KM.3)
- [ ] Skicka förfrågan: antal platser, valfri hälsning
- [ ] Föraren accepterar — platsräkning server-side, accept som spränger antalet avvisas
- [ ] Föraren nekar — **meddelande krävs**, med färdiga formuleringar plus fritext
- [ ] Förfrågan går att skicka även när erbjudandet är fullt; fullt märks tydligt i listan
- [ ] Den som frågat kan återta sin förfrågan; föraren kan dra tillbaka erbjudandet
- [ ] Tränarens överblick per match
- [ ] Fritext loggas aldrig; samåkningen gallras 30 dagar efter matchen (§KM.12)

**Klar när:** två föräldrar kan komma överens om skjuts till en bortamatch utan att skriva i gruppchatten — och den som nekas får veta varför.

---

## M6 — Kallelse (byggd, avstängd)

- [ ] `Team.AttendanceEnabled` — kontrolleras serverside i varje handler, `404` när av (§KM.7)
- [ ] Tränaren kallar till match
- [ ] Föräldern svarar Kommer / Kan inte / Kanske för sitt barn
- [ ] Närvarosummering per match för tränaren
- [ ] Påminn dem som inte svarat
- [ ] Admin kan slå på flaggan per lag
- [ ] Samtyckesrutin innan en trupp läggs upp (§KM.6)

**Klar när:** funktionen fungerar fullt ut i test med flaggan på, och är helt osynlig och otillgänglig med flaggan av.

---

## M7 — Notiser

- [ ] VAPID-nycklar i secret store; prenumerationsregistrering och avregistrering
- [ ] **Anonym prenumeration** på matchändringar per lag — kräver inget konto
- [ ] Bakgrundsjobb för utskick — aldrig i request-tråden
- [ ] Push vid ny, flyttad, ändrad och inställd match
- [ ] Push vid samåkning: nytt erbjudande till laget, ny förfrågan till föraren, svar till den som frågade
- [ ] Schemalagd påminnelse kvällen före match via dygnscron
- [ ] Notis-payload innehåller ingen PII (§KM.1) och aldrig spelarkortsdata (§KM.2)
- [ ] Gallring av döda prenumerationer
- [ ] Notisinställningar per användare och lag
- [ ] Installationstips på iOS, där push kräver hemskärm-installation

**Klar när:** en flyttad match ger en notis inom en minut till dem som tillåtit det, och kvällen före match kommer en påminnelse.

---

## M8 — Lansering

- [ ] Integritetstext skriven för föräldrar, inte jurister
- [ ] Registerutdrag: export av kontots data på begäran *(spelarkortet ligger redan hos familjen — exporteras med säkerhetskopieringskoden)*
- [ ] Gallringsregler implementerade
- [ ] **Hela [`SAKERHET-CHECKLISTA.md`](../SAKERHET-CHECKLISTA.md) avbockad** — inga `➖` på §KM-rader
- [ ] SAST/DAST kört, fynd åtgärdade
- [ ] A11y-genomgång: WCAG 2.1 AA på alla flöden
- [ ] E2E-test av de fem kritiska flödena i `SPEC.md` §9
- [ ] Testad på riktig iPhone och riktig Android
- [ ] Domän kopplad, HTTPS och HSTS verifierat
- [ ] Gamla JSONBin-nyckeln roterad
- [ ] Utrullning: länk till föräldragruppen, kort instruktion för kalenderprenumeration

**Klar när:** föräldrarna har fått länken och den fungerar för dem, inte bara för oss.

---

## Post-MVP (backlog — bygg inte nu)

Tvätt- och kioskschema · delbar matchbild till gruppchatten · fler åldersgrupper i klubben ·
träningar utöver matcher · automatisk schemaimport från seriearrangör · offline-först med synkkö ·
i18n · native app.

---

## Vad som medvetet aldrig byggs

- **Publik skytteliga eller lagtabell** — strider mot barnfotbollens riktlinjer och mot beslutet om privat statistik.
- **Möjlighet för någon roll att läsa en annan familjs statistik** — inklusive Admin. Detta är en invariant, inte en prioritering.
- **Spårning eller besöksanalys.**
