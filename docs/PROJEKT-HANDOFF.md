# PROJEKT-HANDOFF — Kärra Matcher

> **Detta är den första filen som läses i varje ny session** (av människa och agent).
> Den ska alltid spegla **faktisk** status. Uppdateras enligt §0 p.14 i [`CLAUDE.md`](../CLAUDE.md)
> — i samma commit som ett issue stängs.
> Skriv ingen kod förrän ett issue är valt ur `Ready` och godkänt av människa.

---

## 🔎 Snabbstatus
- **Fas:** M0 pågår — 3 av 15 issues klara. Repot är publikt
- **Senast uppdaterad:** 2026-08-29 av Haval
- **Aktuell milstolpe:** M0 — Grund (ej påbörjad)
- **Hälsa:** 🟢 på plan

## 🧱 Teknikstack (bekräftad)
- **Backend:** C# / .NET (senaste LTS), EF Core, MediatR, FluentValidation, PostgreSQL via Npgsql
- **Frontend:** React + TypeScript (Vite), TanStack Router, TanStack Query, React Hook Form + Zod
- **Klient:** PWA — installerbar, offline-läsbart schema. Ingen app store
- **Repo:** Monorepo — `backend/` och `frontend/` i samma repo
- **Notiser:** Web Push (VAPID) + ICS-kalenderfeed per lag
- **Drift:** Frontend på **Vercel**, backend som Docker-container på **Render**, databas på **Neon**.
  `frontend/vercel.json` rewriter `/api/*` till Render → klienten ser en enda origin. Allt på fria nivåer.
  Samma uppsättning som `source/repos/CarCheck` (carcheck.se), som redan är i drift.

## 🔗 Viktiga länkar
| Resurs | Länk |
|--------|------|
| Projektboard (GitHub Projects) | https://github.com/users/Haval-Jalal/projects/7 |
| Repo | https://github.com/Haval-Jalal/KarraMatcher *(publikt)* |
| Regelverk (HUR) | [`CLAUDE.md`](../CLAUDE.md) |
| Produktspec (VAD & VARFÖR) | [`SPEC.md`](../SPEC.md) |
| Standarder vid behov | [`STANDARDER-VID-BEHOV.md`](../STANDARDER-VID-BEHOV.md) |
| Säkerhetschecklista (releasegrind) | [`SAKERHET-CHECKLISTA.md`](../SAKERHET-CHECKLISTA.md) |
| Plan / roadmap | [`MVP-PLAN.md`](./MVP-PLAN.md) |
| Ursprungsmallar | Förvaras utanför repot |
| Förstudieplan (artefakt) | https://claude.ai/code/artifact/9fc04494-e56d-4510-acf7-28231d64955a |
| Miljöer (staging / prod) | *ej uppsatta — M0* |

## ✅ Klart hittills
- `#3` Vite-frontend med strict TypeScript, TanStack Router och Query — 2026-08-29
- `#2` .NET-lösningen med fyra lager, fyra testprojekt och arkitekturtester — 2026-08-29
- `#1` .NET 10 LTS installerat och låst med `global.json` — 2026-08-29
- Repo, projektboard och 73 issues fördelade på nio milstolpar — 2026-08-29
- Genomgång av båda projektmallarna i sin helhet — 2026-08-29
- Produktbeslut fattade (roller, statistikmodell, kallelse avstängd, omfattning) — 2026-08-29
- Regelverk, spec, standarder och säkerhetschecklista skrivna och anpassade — 2026-08-29

## 🚧 Pågår nu
| Issue | Vem | Branch | Status |
|-------|-----|--------|--------|
| — | — | — | Inget påbörjat |

## ➡️ Nästa steg
*(M0 — Grund. Måste göras i ordning; de tre första är blockerare för allt annat.)*

1. **Installera .NET LTS-SDK.** Maskinen har 9.0.309, som är STS och vars stödperiod löpte ut i maj 2026 — det bryter mot säkerhetschecklistan rad 10.4. Nytt projekt startas på LTS.
2. **`git init` + GitHub-repo + projektboard** med kolumnerna `Backlog → Ready → In Progress → In Review → Done`.
3. **Konton:** Vercel, Render och Neon (alla fria nivåer) + ett gratis uppetidsverktyg som pingar `/health`.
4. Skapa .NET-lösningen med de fyra lagren och fyra testprojekten enligt `CLAUDE.md`.
5. Skapa Vite-frontenden med feature-struktur, path alias och strict TS.
6. `.editorconfig`, ESLint, Prettier, `.env.example`, CI som kör build, test och lint.
7. Databas + första migration + idempotent seed av 4 lag, 7 spelplatser och 25 matcher.
8. Health checks, ProblemDetails, Serilog med correlation-ID, rate limiting.
9. Dockerfile (multi-stage, non-root, port 8080) + `frontend/vercel.json` med `/api/*`-rewrite — mönstret från CarCheck.
10. DB-backup uppsatt **och återställning testad en gång** (§KM.0 A2).

## 🧭 Viktiga beslut (ADR-light)

| Datum | Beslut | Motivering | Konsekvens |
|-------|--------|------------|-----------|
| 2026-08-29 | **Bas = webb-PWA**, mallen `StrukturBackendFrontend` | Föräldrar installerar inte en app från App Store för ett matchschema; en länk räcker och uppdateringar slår igenom direkt | Ingen app store-avgift eller granskning. Push kräver hemskärm-installation på iOS — kalenderfeeden är fallbacken |
| 2026-08-29 | **Backend = C# / .NET** enligt mallen | Regelverkets backend-kapitel följs rakt av; kompetens och SDK finns | Driftas som Docker-container på Render, inte på Vercel — se nästa rad |
| 2026-08-29 | **Drift = Vercel + Render + Neon med `/api/*`-rewrite** | Samma uppsättning som carcheck.se, som redan kör i produktion. Noll kostnad, monorepo behålls, .NET-backenden behålls | Klienten ser **en enda origin** → CORS behöver inte öppnas och refresh-cookien blir förstapart. Priset är Renders kallstart, som måste maskeras med edge-cache (§KM.11) |
| 2026-08-29 | **Databasen på Neon, inte på Render** | Renders gratisdatabas upphör efter 30 dagar | Familjernas statistik ligger inte på något som självdör |
| 2026-08-29 | **Monorepo** i stället för två repon | Ensam utvecklare; mallen tillåter uttryckligen monorepo. En PR kan röra både BE och FE | §0 p.13 tolkas som "BE-gapet först i egen commit inom samma PR" |
| 2026-08-29 | **Steg 0 omskrivet** för ideell app | Originalfiltren mäter betalningsvilja; ingen betalar för den här appen | Grinden mäter i stället adoption. Originalfiltren finns i projektmallen, utanför repot |
| 2026-08-29 | **Full process** enligt mallen | Målet är ett komplett och granskningsbart projekt | Board, issue, branch och PR för varje ändring — även små |
| 2026-08-29 | **Statistik privat per familj**, ingen skytteliga | Svensk fotbolls riktlinjer avråder från resultatrapportering och tabeller upp till 12 år; minskar dessutom föräldrapress | Matchresultatet skrivs in av varje familj för sig. Ingen roll, inte ens Admin, kan läsa det |
| 2026-08-29 | **Barnstatistiken lagras enbart på enheten** — ingen tabell, ingen endpoint | Spelarkortet är tänkt som något föräldern och barnet gör tillsammans efter matchen. Data som aldrig når servern kan inte läcka från den, och kräver varken konto eller samtycke | Servern behandlar **inga uppgifter om barn** vid lansering. Priset: datan går förlorad vid telefonbyte utan säkerhetskopia → backupkod, `storage.persist()` och installationsuppmaning blir funktionskrav (§KM.2) |
| 2026-08-29 | **Samåkning: förfrågan → accept eller nekande med meddelande** | Föraren ska själv få välja vem som åker med, och ett tyst nej fungerar inte mellan grannar som möts på planen nästa lördag | Ny entitet `CarpoolRequest` med tillståndsmaskin. Nekande utan meddelande avvisas server-side. Kräver inloggning — gäster ser men deltar inte (§KM.12) |
| 2026-08-29 | **Milstolparna omordnade till M0–M8** | Inloggningen behövs av tränaradmin och samåkning men inte längre av statistiken | Konto och roller blev en egen milstolpe (M2) före tränaradmin (M3). Spelarkortet (M4) är helt frikopplat och kan byggas parallellt |
| 2026-08-29 | **Öppen läsning, autentiserad skrivning** | En förälder som bara vill se matchtiden ska aldrig mötas av inloggning | Avvikelse §KM.0 A4. Kräver rate limiting från start och att publika endpoints aldrig returnerar PII |
| 2026-08-29 | **Kallelse byggs men aktiveras inte** | Det finns redan appar för kallelse; funktionen ska finnas den dag klubben vill byta | `Team.AttendanceEnabled` kontrolleras serverside. Ingen komplett trupp krävs vid lansering |
| 2026-08-29 | **Repot är publikt** | Ingen anledning att hålla en ideell klubbapp stängd. Beslutet togs efter att innehållet i den borttagna mallmappen granskats i detalj | Branch protection är nu gratis om den ska slås på. **Påverkar mediator-valet:** RPL-1.5 kräver källkodspublicering vid driftsättning — med publikt repo är det uppfyllt, men vår egen kod skulle då hamna under copyleft |
| 2026-08-29 | **Branch protection skjuts upp** | Ensam utvecklare, och `.githooks/pre-push` räcker | Kan slås på när som helst nu när repot är publikt |
| 2026-08-29 | **`mallar/` borttagen ur hela historiken** | Mappen innehöll en affärsplan för en orelaterad produkt och hörde inte hemma i det här repot | Historiken är omskriven och force-pushad. Se känd risk nedan om gamla objekt |
| 2026-08-29 | **M3 avblockerad** — klubbens officiella verktyg används inte, och tränarna vill ha appen | Filter 3 och Filter 4 i `SPEC.md` är därmed besvarade utan villkor. Vi ersätter en oanvänd lösning i stället för att konkurrera med en levande vana | Tränaradmin kan byggas utan förbehåll. Projektets största risk — att appen står tom — är kraftigt reducerad, men adoptionen ska ändå mätas efter lansering |
| 2026-08-29 | **Rate limiting och DB-backup lyfts till baslinje** | Publika endpoints på öppet internet; familjestatistik går inte att återskapa | Avvikelse §KM.0 A1 och A2 — byggs i M0–M1 i stället för "vid behov" |

## ❓ Öppna frågor

| # | Fråga | Blockerar | Ägare |
|---|-------|-----------|-------|
| 1 | ~~Har klubben redan ett officiellt verktyg?~~ **Besvarad 2026-08-29:** ja, men det används inte. | — | ✅ |
| 2 | ~~Vill tränarna använda appen?~~ **Besvarad 2026-08-29:** ja. | — | ✅ |
| 2b | **Var kommer matchschemat ifrån i dag?** Avgör om automatisk import är värd att bygga senare. Icke-blockerande. | — | Haval |
| 2c | **Vilken mediator ska Application använda?** MediatR 13+ ligger under RPL-1.5 eller kommersiell licens. RPL kräver källkodspublicering vid **driftsättning**, inte bara distribution — vilket krockar med ett privat repo som servar en publik app. Alternativ: handskriven dispatcher (~40 rader, rekommenderat), MediatR 12.4.1 fastlåst (Apache-2.0 men fryst), eller acceptera RPL-copyleft på vår egen kod. **Behövs innan första handlern.** | M3 | Haval |
| 3 | ~~Var driftas backend?~~ **Besvarad 2026-08-29:** Vercel + Render + Neon, se ADR ovan. | — | ✅ |
| 4 | **Vilken e-postleverantör** för inloggningskoden? Måste ha EU-hosting och gratisnivå. | M3 | Haval |
| 5 | **Domännamn** — `karramatcher.se` föreslaget, tillgänglighet ej kontrollerad. | M7 | Haval |
| 6 | **Vem är admin och vilka är tränare?** Riktiga personer krävs före lansering. | M7 | Haval |
| 7 | **Samtyckestexten** — behövs först när en tränare lägger upp truppen, eftersom servern annars inte behandlar några barnuppgifter alls. Kvar: en begriplig integritetstext. | M6 | Haval + klubben |
| 9 | **Hur hittar föräldrar varandra vid samåkning?** Vi lagrar inga telefonnummer. Räcker namn plus meddelandefältet? | M5 | Haval — pröva med tränarna |
| 8 | **Kärra KIF:s namn och märke** — får appen använda dem, eller ska den vara tydligt inofficiell? | M7 | Haval |

## ⚠️ Kända risker & blockerare

| Risk | Konsekvens | Plan |
|------|-----------|------|
| **Tränarna använder den inte** | Appen är tom och allt annat spelar ingen roll. Detta är den enskilt största risken. | Förankra före M2. Massinlägget måste vara så snabbt att det slår gruppchatten första gången de provar |
| **Säsongsbunden användning** | Appen glöms bort mellan säsongerna | Kalenderfeed och push levererar värde utan att appen öppnas — därför ligger de i v1 |
| **Renders kallstart** | Backend somnar efter ca 15 min tystnad och tar ~50 s att vakna. Appen används mest lördag morgon, efter en tyst natt — första föräldern får vänta | Publika GET-svar cachas på Vercels edge så vanliga sidvisningar aldrig väcker backend; ett gratis uppetidsverktyg pingar `/health`; UI:t säger ifrån på svenska vid långsamt svar (§KM.11) |
| **iOS-push kräver hemskärm-installation** | En del föräldrar får aldrig notiser | Kalenderfeeden är den primära kanalen; push är komplement. Installationstips visas på iOS |
| **Barn-PII** | Ett läckage av barnuppgifter vore allvarligt, oavsett hur litet projektet är | Kraftigt reducerad: spelarkortet når aldrig servern (§KM.2), och truppen finns bara om kallelsen aktiveras. §KM.1 sätter taket på det som ändå lagras |
| **Spelarkortet kan gå förlorat** | Telefonbyte, rensad webbläsardata eller iOS som gallrar lagring för en app som inte använts på en vecka → en hel säsong borta | Säkerhetskopieringskod som förstaklassfunktion, `navigator.storage.persist()`, tydlig uppmaning att installera på hemskärmen, och ärlig text om var datan finns (§KM.2) |
| **Gamla JSONBin-nyckeln lever kvar** | Master-nyckel i en telefons localStorage ger åtkomst till hela JSONBin-kontot | Rotera nyckeln på jsonbin.io. Checklistan rad 7.7 |
| **Ingen branch protection på GitHub** | Privat repo utan GitHub Pro kan inte skydda `main`. Ett misstag kan pusha direkt förbi PR-flödet | **Accepterad risk tå vidare** (beslut 2026-08-29). `.githooks/pre-push` blockerar push till `main` lokalt — aktiveras med `git config core.hooksPath .githooks`, en gång per klon. Riktig branch protection kommer gratis när repot blir publikt vid lansering |
| **Gamla objekt kvar på GitHub efter historikomskrivning** | Att skriva om historik raderar inte gamla objekt hos GitHub — de nås via sina SHA:n tills GitHub kör städning, och SHA:na syns i commitlistorna på mergade PR:ar. Verifierat: den borttagna filen gick att hämta via `?ref=<gammal SHA>` även efter omskrivningen | **Accepterad risk** (beslut 2026-08-29) — innehållet granskades och bedömdes okritiskt. **Lärdom:** kontrollera vad en mapp innehåller *innan* den checkas in; en omskrivning i efterhand är aldrig fullständig utan att be GitHub köra `gc` |
| **Licensfläck från ett beroende** | Ett copyleft-licensierat bibliotek kan tvinga fram publicering av vår källkod — RPL-1.5 redan vid driftsättning | Kontrollera licensen innan ett paket läggs in, inte efteråt. Upptäcktes på MediatR i #2, se öppen fråga 2c |
| **Ensam utvecklare** | Ingen annan kan ta över, och PR-granskning görs av samma person som skrev koden | Handoff-filen och ADR-tabellen hålls aktuella så en ny person kan kliva in. `/code-review` och `security_reviewer` används som andra ögon |
