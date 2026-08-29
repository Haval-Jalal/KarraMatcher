# PROJEKT-HANDOFF — Kärra Matcher

> **Detta är den första filen som läses i varje ny session** (av människa och agent).
> Den ska alltid spegla **faktisk** status. Uppdateras enligt §0 p.14 i [`CLAUDE.md`](../CLAUDE.md)
> — i samma commit som ett issue stängs.
> Skriv ingen kod förrän ett issue är valt ur `Ready` och godkänt av människa.

---

## 🔎 Snabbstatus
- **Fas:** **M0 klar (15/15).** M1 pågår — 2 av 14 issues. Repot är publikt
- **Senast uppdaterad:** 2026-08-29 av Haval
- **Aktuell milstolpe:** M1 — Den publika delen
- **Hälsa:** 🟢 på plan — backend är i drift på Render och svarar `Healthy` mot Neon

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
| Backup och återställning | [`DATABAS-BACKUP.md`](./DATABAS-BACKUP.md) — läses när något gått fel |
| Driftövervakning | [`DRIFTOVERVAKNING.md`](./DRIFTOVERVAKNING.md) — pingschema och timbudget |
| Ursprungsmallar | Förvaras utanför repot |
| Förstudieplan (artefakt) | https://claude.ai/code/artifact/9fc04494-e56d-4510-acf7-28231d64955a |
| Backend i drift (Render) | Live sedan 2026-08-29. URL:en finns på exakt ett ställe: [`frontend/vercel.json`](../frontend/vercel.json) (§KM.11) |
| Frontend i drift (Vercel) | https://karra-matcher.vercel.app — live sedan 2026-08-29 |

## ✅ Klart hittills
- `#92` Edge-cachningen verifierad i skarp drift — §KM.11:s antagande bevisat — 2026-08-30
- `#16` Publika endpoints för lag och matcher, med egen query-dispatcher — 2026-08-30
- `#15` Uppetidsövervakning med verifierat larm — **M0 därmed klar** — 2026-08-30
- `#14` Databasbackup med bevisad återställning; PITR 6 h bekräftad — 2026-08-29
- `#13` Edge-cache-mekanism med ETag och säker standard — 2026-08-29
- `#10` Arkitekturtester: lagergränser, entiteter i controllers, §KM.2-skyddet — 2026-08-29
- `#12` `vercel.json` med `/api`-rewrite; frontend i drift på Vercel — 2026-08-29
- `#11` Dockerfile och backend i drift på Render — 2026-08-29
- `#9` Rate limiting på publika endpoints — 2026-08-29
- `#8` Serilog, correlation-ID, ProblemDetails och health checks — 2026-08-29
- `#7` Idempotent seed av 4 lag, 7 spelplatser och 25 matcher — 2026-08-29
- `#6` Neon Postgres och EF Core med första migrationen — 2026-08-29
- `#5` CI som kör bygge, tester och lint på varje push — 2026-08-29
- `#4` Lint, formatering och `.env.example` — 2026-08-29
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
| `#92` Verifiera edge-cachningen | Haval | `feature/verify-edge-cache` | In Review — verifierad i skarp drift |

## ➡️ Nästa steg
*(Kvar i M0 — Grund.)*

1. **`#27` FE-halvan, sedan `#18` och `#19`** — lagväljare och matchlista. Backendens tidszonshantering är redan klar, så `#27` är mindre än den ser ut.
2. **`#17` och `#21`** — matchdetalj. `#17` måste ta uttrycklig ställning till om matchnotisen hör hemma i ett publikt svar (§KM.1).

När M0 är stängd tar M1 vid enligt [`MVP-PLAN.md`](./MVP-PLAN.md).

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
| 2026-08-30 | **Handskriven query-dispatcher i stället för MediatR** (öppen fråga 2c besvarad) | MediatR 13+ ligger under RPL-1.5, som utlöses vid **driftsättning** och skulle lägga vår egen kod under copyleft. MediatR 12.4.1 är Apache-2.0 men fryst sedan 2024. Dispatchern är ~70 rader med omslagscache och en behavior-kedja | Avvikelse **§KM.0 A9**. Noll licensyta och inget beroende som kan ändra villkor mitt i projektet. Priset: vi underhåller den själva och pipeline-behaviors byggs efter hand — därför är dispatchern täckt av egna tester som bevisar uppslagning, ordning och att valideringen avbryter |
| 2026-08-29 | **Uppetidspingen fönstras i stället för att gå dygnet runt** | Render free ger 750 instanstimmar per månad och arbetsyta, och suspenderar **alla** fria tjänster resten av månaden när de tar slut. En ping var femte minut dygnet runt förbrukar 744 timmar i en 31-dagarsmånad — sex timmars marginal. Det är ett dåligt byte för en app hundra familjer förlitar sig på | Pingas var 5:e minut fredag 15:00 till söndag 22:59, plus en daglig kontroll 14:50. ~251 h/månad, ~499 h marginal. Priset: en tränare som redigerar en vardagskväll kan möta ~1 minuts kallstart, och avbrott utanför helgen upptäcks inom ett dygn. Rutinen står i `docs/DRIFTOVERVAKNING.md` |
| 2026-08-29 | **Databasdumpar tas manuellt, aldrig i CI** | En schemalagd GitHub Actions-körning hade varit lätt att skriva, men artefakter i ett publikt repo går att ladda ner av vem som helst med länken. Så snart en tränare lägger upp truppen ligger barns förnamn i databasen (§KM.1) | Neons PITR är det automatiska lagret. Den logiska dumpen tas av en människa, till en katalog utanför repot. `.gitignore` blockerar `*.dump` som skyddsnät |
| 2026-08-29 | **Cachning är opt-in, inte opt-out** | Standarden för varje svar är `private, no-store`; en endpoint blir publikt cachebar först genom att uttryckligen säga det. Motsatt standard hade gjort en glömd markering till ett dataläckage i stället för till en missad optimering | Varje publik endpoint i M1 måste komma ihåg `.WithEdgeCache(...)`, annars väcks Render i onödan. Priset är medvetet: att glömma kostar prestanda, aldrig integritet |
| 2026-08-29 | **Arkitekturtesterna skrivs för hand — NetArchTest väljs bort** | Paketet som issue #10 namnger har inte släppts sedan maj 2021 och **deklarerar ingen licens i paketmetadatan** (GitHub-repot uppger MIT, nuspec:en är tom). Efter MediatR-överraskningen är ett olicensierat, övergivet beroende inte värt bekvämligheten. Alternativen — eNhancedEdition (MIT) och ArchUnitNET (Apache-2.0) — hade fungerat, men behövdes inte | Noll ny licensyta. De befintliga handskrivna reglerna täckte redan två av issuets tre krav; det tredje blev ~60 rader reflektion. Priset: vi underhåller detektorerna själva, vilket motiverar självtesterna som bevisar att varje regel faktiskt faller |
| 2026-08-29 | **Rate limiting och DB-backup lyfts till baslinje** | Publika endpoints på öppet internet; familjestatistik går inte att återskapa | Avvikelse §KM.0 A1 och A2 — byggs i M0–M1 i stället för "vid behov" |

## ❓ Öppna frågor

| # | Fråga | Blockerar | Ägare |
|---|-------|-----------|-------|
| 1 | ~~Har klubben redan ett officiellt verktyg?~~ **Besvarad 2026-08-29:** ja, men det används inte. | — | ✅ |
| 2 | ~~Vill tränarna använda appen?~~ **Besvarad 2026-08-29:** ja. | — | ✅ |
| 2b | **Var kommer matchschemat ifrån i dag?** Avgör om automatisk import är värd att bygga senare. Icke-blockerande. | — | Haval |
| 2c | ~~**Vilken mediator ska Application använda?**~~ **Besvarad 2026-08-30:** handskriven dispatcher, se ADR ovan. Tidigare formulering: MediatR 13+ ligger under RPL-1.5 eller kommersiell licens. RPL kräver källkodspublicering vid **driftsättning**, inte bara distribution — vilket krockar med ett privat repo som servar en publik app. Alternativ: handskriven dispatcher (~40 rader, rekommenderat), MediatR 12.4.1 fastlåst (Apache-2.0 men fryst), eller acceptera RPL-copyleft på vår egen kod. **Behövdes innan första handlern**, vilket visade sig vara `#16` i M1 — inte M3. | — | ✅ |
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
| **Ingen branch protection på GitHub** | `main` är oskyddad på GitHub-sidan. Ett misstag kan pusha direkt förbi PR-flödet | **Accepterad risk tills vidare** (beslut 2026-08-29). `.githooks/pre-push` blockerar push till `main` lokalt — aktiveras med `git config core.hooksPath .githooks`, en gång per klon. Repot är numera publikt, så riktig branch protection är gratis och kan slås på när som helst |
| **Gamla objekt kvar på GitHub efter historikomskrivning** | Att skriva om historik raderar inte gamla objekt hos GitHub — de nås via sina SHA:n tills GitHub kör städning, och SHA:na syns i commitlistorna på mergade PR:ar. Verifierat: den borttagna filen gick att hämta via `?ref=<gammal SHA>` även efter omskrivningen | **Accepterad risk** (beslut 2026-08-29) — innehållet granskades och bedömdes okritiskt. **Lärdom:** kontrollera vad en mapp innehåller *innan* den checkas in; en omskrivning i efterhand är aldrig fullständig utan att be GitHub köra `gc` |
| **Licensfläck från ett beroende** | Ett copyleft-licensierat bibliotek kan tvinga fram publicering av vår källkod — RPL-1.5 redan vid driftsättning | Kontrollera licensen innan ett paket läggs in, inte efteråt. Upptäcktes på MediatR i #2, se öppen fråga 2c |
| **Renders timbudget kan släcka appen** | 750 fria instanstimmar per månad och arbetsyta. När de tar slut suspenderas alla fria tjänster till den 1:a nästa månad — appen nere i upp till 30 dagar | Fönstrad ping håller förbrukningen kring 251 h/månad. **Ingen andra fri Render-tjänst får läggas till utan att räkna om budgeten.** Kontrollera förbrukningen i Renders dashboard inför säsongsstart |
| **Neons retentionsfönster är 6 timmar** | Bekräftat 2026-08-29 — det är taket på fri nivå. En trasig migration som driftsätts fredag kväll och upptäcks lördag morgon ligger redan utanför fönstret, och det är precis då appen används | Den logiska dumpen är därmed det egentliga skyddsnätet, inte en reserv. Tas före **varje** migration mot produktion och efter att säsongens schema lagts in. Rutinen står i `docs/DATABAS-BACKUP.md`. Längre fönster kräver betald Neon-plan — omprövas om datamängden växer |
| ~~**Vercel kanske inte edge-cachar en extern rewrite**~~ **Avskriven 2026-08-30** | Antagandet höll. Två anrop i rad gav `MISS` följt av `HIT`, med **identiskt `Rndr-Id`** — Render utfärdar ett nytt id per request, så backend kontaktades aldrig på det andra anropet | Verifieringen står i `README.md`. Kvarstår att veta: Vercel försvagar vår ETag till `W/"…"`, vilket vår `If-None-Match`-tolkning hanterar. Tas den hanteringen bort slutar `304` fungera i drift utan att CI märker det |
| **Ensam utvecklare** | Ingen annan kan ta över, och PR-granskning görs av samma person som skrev koden | Handoff-filen och ADR-tabellen hålls aktuella så en ny person kan kliva in. `/code-review` och `security_reviewer` används som andra ögon |
