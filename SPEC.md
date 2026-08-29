# SPEC.md — Produktspecifikation: **Kärra Matcher**

> **Hur den här filen används:** Detta beskriver **VAD** som ska byggas och **VARFÖR**.
> [`CLAUDE.md`](./CLAUDE.md) beskriver **HUR** (arkitektur, standarder, säkerhet). Agenten läser båda.
>
> **Arbetsordning:** Steg 0 är grinden. Passerar problemet inte den, bygger vi inte.

---

# 🎯 STEG 0 — Validera problemet: fyra filter

> **Anpassad grind.** Mallens fyra filter (Painful, Frequent, Large Enough, Underserved) mäter
> **betalningsvilja**. Kärra Matcher är en ideell app för en fotbollsförening — ingen ska betala för den.
> Filtren nedan mäter i stället det som faktiskt avgör om appen lyckas: **blir den använd?**
> Originalfiltren finns kvar i `mallar/StrukturBackendFrontend/SPEC.md` för jämförelse.

| # | Filter | Frågan | Svar |
|---|--------|--------|------|
| 1 | **Verklig friktion** | Lägger någon tid eller irritation på problemet idag? | ✅ Ja |
| 2 | **Återkommande** | Uppstår det varje vecka — inte en gång per säsong? | 🟡 Ja under säsong, noll utanför |
| 3 | **Når vi alla** | Kan vi faktiskt nå varje familj i laget? | ✅ Ja |
| 4 | **Bättre än dagens** | Är dagens lösning sämre — strukturellt, inte bara fulare? | ✅ Ja, villkorat |

## Filter 1 — Verklig friktion 🔥
**Test:** Vad *gör* de åt problemet idag? "Inget" = inbillad friktion. "Byggt en workaround" = verklig.

**▶ Svar:** Verklig, och redan bevisad. Tränarna sprider matchtider och ändringar i föräldragruppen,
där de drunknar bland annat prat. Föräldrar frågar om samma tid flera gånger, och någon kommer fel
varje gång en match flyttas. Det starkaste beviset är att **en förälder redan byggt en egen app i
telefonen** (`index 4.html`, 687 rader) för att slippa problemet — ingen bygger en workaround åt
något som inte skaver. Smärtan är måttlig men äkta och drabbar alla i laget, inte bara en person.
**Pass.**

## Filter 2 — Återkommande 🔁
**Test:** Veckovis under säsong räcker — men vad händer mellan säsongerna?

**▶ Svar:** Under seriespel: 1–2 matcher per vecka och lag. Föräldern kollar inför varje match och
varje gång något ändras; tränaren redigerar schemat ungefär varannan vecka. Det är **veckovis** och
tillräckligt.
**Ärlig invändning:** frekvensen är säsongsbunden. Mellan höst- och vårsäsong är den nära noll, och en
app man inte öppnat på fyra månader glöms bort. **Motåtgärd, inbyggd i scopet:** kalenderprenumerationen
och push gör att appen levererar värde **utan att öppnas** — matchen dyker upp i telefonens egen
kalender oavsett. Det är därför de två funktionerna ligger i v1 och inte i backloggen.
**Delvis pass (🟡) — löst genom att värdet inte kräver att appen öppnas.**

## Filter 3 — Når vi alla 📈
**Test:** En app som halva laget använder är sämre än ingen app — då måste tränaren informera i två kanaler.

**▶ Svar:** Målgruppen är avgränsad och nåbar: **4 lag i P2016, ca 50–60 barn, ca 100–130 föräldrar**
plus mor- och farföräldrar som hämtar och skjutsar. De nås via den befintliga föräldragruppen och via
tränarna — ingen marknadsföring behövs, en länk räcker.
**Adoptionsmål:** alla fyra tränare lägger in sitt schema i appen, och ≥ 60 % av familjerna har öppnat
den inom fyra veckor från lansering. Nås inte tränardelen är appen tom och allt annat spelar ingen roll.
**Pass.**

## Filter 4 — Bättre än dagens 🎯
**Test:** Varför är det här strukturellt bättre än gruppchatten och klubbens befintliga verktyg?

**▶ Svar:** Strukturell fördel = **Simplicity + rätt kanal**.
- **Ingen inloggning** för att se en matchtid. Varje befintligt verktyg i den här kategorin kräver konto.
- **Kalenderprenumeration** gör att föräldern slipper appen helt — matcherna hamnar i telefonens
  egen kalender och flyttar sig av sig själva. Det är inget gruppchatten kan göra.
- **En sak, gjord ordentligt:** matcher. Inte medlemsregister, avgifter och nyheter.

**⚠️ Ärlig invändning:** klubben kan redan ha ett officiellt verktyg (laget.se, Svenskalag eller
liknande). Konkurrerar vi med det får tränarna två ställen att underhålla, och då förlorar vi.
**Detta måste kontrolleras med tränarna innan tränaradmin (M3) påbörjas** — se Öppna frågor.
**Pass, villkorat.**

## ✅ Filter-scorecard (grind innan bygge)

| Filter | Pass? | Kommentar |
|--------|:-----:|-----------|
| 1. Verklig friktion | **J** | Bevisad — en workaround finns redan byggd |
| 2. Återkommande | 🟡 | Veckovis under säsong, noll utanför → värdet får inte kräva att appen öppnas |
| 3. Når vi alla | **J** | 4 lag, ~130 föräldrar, nåbara via en länk |
| 4. Bättre än dagens | **J** | Ingen inloggning + kalenderfeed. Villkor: klubbens befintliga verktyg måste kartläggas |

> **Beslut:** Passerar med **tre villkor** som gäller genom hela bygget:
> 1. **Kalenderprenumeration och push ligger i v1** — annars faller Filter 2.
> 2. **Tränarna förankras innan M3** — vill de inte lägga in schemat är appen tom (Filter 3).
> 3. **Klubbens befintliga verktyg kartläggs** innan vi bygger tränardelen (Filter 4).
>
> Grönt att fortsätta till produktspecen.

---

# 📋 PRODUKTSPECIFIKATION

## 1. Vision & mål
- **One-liner:** Alla matchtider för Kärra P2016 på ett ställe — utan inloggning för den som bara vill veta när och var, och med ett verktyg som gör att tränaren kan ändra schemat på en halv minut.
- **Mål:** Ta bort matchlogistiken ur föräldragruppen, och ge varje barn ett eget litet spelarkort med sina mål, assist och märken.
- **Framgångsmått:**
  - Alla fyra tränare underhåller sitt schema i appen (binärt — det här är det som avgör allt).
  - ≥ 60 % av familjerna har öppnat appen inom fyra veckor.
  - ≥ 30 % av familjerna prenumererar på kalenderfeeden.
  - Noll rapporterade fall av "vi kom till fel plan" efter lansering.

## 2. Målgrupp & kund
- **Primär målgrupp:** Föräldrar till barn i Kärra P2016 (fyra lag: Gul, Blå, Vit, Svart). Blandad teknikvana — allt från utvecklare till mor- och farföräldrar som knappt installerar appar.
- **Sekundär, men avgörande:** de fyra tränarna. De är inte betalande kunder, men de är den som fyller appen med innehåll. Går den inte snabbt för dem, dör produkten.
- **Användarkontext:** På språng, ofta i mobilen, ofta med en hand och ett barn i den andra. Ibland på en fotbollsplan med dålig täckning. Ofta kvällen innan match.
- **Köpare vs användare:** Ingen betalar. Klubben/föräldragruppen "äger" appen socialt; en förälder driftar den.

## 3. Problem & lösning
- **Problem:** Matchtider, platser och ändringar sprids i en gruppchatt där de försvinner. Tränaren måste upprepa sig, föräldrar frågar om samma sak, och en flyttad match når inte alla i tid.
- **Dagens alternativ:** Föräldragruppen i chattappen · klubbens eventuella officiella verktyg · den handbyggda `index 4.html` · kylskåpsdörren.
- **Vår lösning:** En öppen länk med lagets schema, väder och vägbeskrivning; en kalenderfeed som håller föräldrarnas egna kalendrar uppdaterade automatiskt; ett enkelt tränarverktyg med massinlägg; och privat statistik per barn.

## 4. Omfattning (scope)

**MVP — ingår:**
Lagvis matchschema (kommande och tidigare) · matchdetalj med väder, karta och kalender ·
**ICS-prenumeration per lag** · tränaradmin: skapa, ändra, ta bort, ställa in match ·
**massinlägg av helt schema med förhandsgranskning** · trupp (förberedd) · konto via e-postkod ·
**spelarkort helt på egen enhet** (resultat, mål, assist, märken, säsongssammanfattning) ·
**säkerhetskopiering och återställning av spelarkortet** · **samåkning med förfrågan och
accept eller nekande med meddelande** · **Web Push vid ändring och påminnelse dagen före** ·
installerbar PWA med offline-läsbart schema · import av gammal `KARRA1.`-backupkod.

**Byggt men avstängt (feature flag per lag):**
Kallelse och närvaro · närvarosummering · påminn dem som inte svarat · tränarens truppvy.

**Ingår INTE (nu):**
Publik lagtabell eller skytteliga · medlemsavgifter · närvarostatistik på träningar · chatt i appen ·
native app i App Store/Play · flera klubbar · betalningar.

**Framtida (backlog):**
Tvätt- och kioskschema · delbar matchbild till gruppchatten · fler åldersgrupper i klubben ·
säsongssammanfattning som delningsbar bild · träningar utöver matcher.

## 5. Features / användarberättelser

| Prio | User story | Acceptanskriterier |
|------|-----------|--------------------|
| Must | Som förälder vill jag se mitt lags kommande matcher utan att logga in, så att jag snabbt vet när och var. | Öppen länk visar lagets matcher sorterade i tid, med datum, tid, motståndare, plats och hemma/borta. Inget konto krävs. |
| Must | Som förälder vill jag se nästa match överst med relativ dag, så att jag direkt ser vad som gäller. | "Nästa match" visar motståndare, tid, plats och "idag / i morgon / om N dagar". |
| Must | Som förälder vill jag få vägbeskrivning till planen, så att jag hittar dit. | Knapp öppnar Apple Maps på iOS och Google Maps annars, med planens adress. |
| Must | Som förälder vill jag se vädret vid avspark, så att jag vet hur barnet ska klä sig. | Temperatur, nederbördssannolikhet och symbol för matchens klockslag, för matcher inom 15 dagar. |
| Must | Som förälder vill jag prenumerera på lagets matcher i min egen kalender, så att jag slipper öppna appen. | `webcal://`-länk per lag. Ny match dyker upp automatiskt; ändrad match uppdateras; inställd match visas som inställd. |
| Must | Som tränare vill jag lägga till, ändra, ta bort och ställa in en match, så att schemat stämmer. | Inloggad tränare med rätt lag kan göra alla fyra. Ändringen syns för alla direkt och audit-loggas. |
| Must | Som tränare vill jag klistra in hela serieschemat på en gång, så att jag slipper mata in 25 matcher. | Inklistrad text tolkas, visas som förhandsgranskningstabell med status per rad (klar / saknar uppgift / dubblett), och sparas först efter godkännande. |
| Must | Som förälder vill jag skapa ett konto med e-post, så att mitt barns statistik följer med till en ny telefon. | Kod skickas till e-post, ingen lösenordshantering. Lokalt sparad statistik flyttas upp vid första inloggning. |
| Must | Som förälder vill jag lägga till mitt barn, så att jag kan föra dess statistik. | Barnet sparas med förnamn och valfritt tröjnummer, kopplat till mig som vårdnadshavare, efter accepterat samtycke. |
| Must | Som förälder vill jag fylla i matchresultat och mitt barns mål och assist tillsammans med barnet efter matchen, så att det blir en rolig stund och en säsong att följa. | Sparas **enbart på den egna enheten**. Inget konto krävs. Inget skickas någonstans. |
| Must | Som förälder vill jag kunna säkerhetskopiera spelarkortet, så att det inte försvinner när jag byter telefon. | En kod kan kopieras och klistras in igen på en annan enhet. Appen påminner om att spara den. |
| Must | Som barn vill jag se mina mål, assist och märken, så att det blir roligt. | Spelarkort med totaler, upplåsta och låsta märken samt en rad per spelad match. |
| Must | Som förälder vill jag få en notis när en match ändras eller ställs in, så att jag inte kommer fel. | Web Push till dem som tillåtit notiser. Skickas vid ny, flyttad, ändrad och inställd match. |
| Must | Som förälder vill jag få en påminnelse kvällen före match. | Schemalagt jobb skickar påminnelse för morgondagens matcher till lagets prenumeranter. |
| Must | Som inloggad förälder vill jag lägga upp att jag har plats för 1–4 personer till en match, så att andra kan åka med. | Erbjudande med riktning, avgångsplats, avgångstid, antal platser och valfri notis. Lagets prenumeranter får en notis. |
| Must | Som inloggad förälder vill jag skicka en förfrågan om att få åka med, så att jag slipper fråga i gruppchatten. | Förfrågan med antal platser och valfri hälsning. Föraren får en notis. |
| Must | Som förare vill jag själv acceptera eller neka en förfrågan, med ett meddelande när jag nekar. | Endast den som lagt upp erbjudandet kan svara. **Nekande kräver ett meddelande** — färdiga formuleringar plus fritext. Den som frågade får en notis med svaret. |
| Must | Som gäst vill jag kunna se vilka som erbjuder skjuts, men förstå att jag måste logga in för att delta. | Erbjudanden syns utan konto. Knappar för att lägga upp eller fråga leder till inloggning — aldrig till ett tyst fel. |
| Should | Som förälder vill jag kunna använda appen utan nät på planen. | Lagets schema och appskalet är läsbara offline; skrivningar visar tydligt att de kräver nät. |
| Should | Som förälder vill jag importera min gamla backupkod, så att inget jag redan fyllt i går förlorat. | En `KARRA1.`-kod kan klistras in och mappas till konto och barn. |
| Could | Som tränare vill jag kalla till match och se vilka som kommer. | Byggd bakom `AttendanceEnabled` per lag. Avstängd vid lansering. |
| Won't (nu) | Publik skytteliga eller lagtabell. | — Strider mot barnfotbollens riktlinjer och mot beslutet om privat statistik. |

## 6. Domänmodell / entiteter

**Entiteter & relationer:**

```
Club 1—* AgeGroup 1—* Team 1—* Match *—1 Venue
Team 1—* Player 1—* Guardian *—1 User
Team 1—* TeamRole *—1 User
Match 1—* Attendance *—1 Player               ← bakom feature flag
Match 1—* CarpoolOffer 1—* CarpoolRequest *—1 User
User 1—* PushSubscription
AuditLogEntry (fristående)

── Enbart på enheten, aldrig i databasen (§KM.2) ──
LocalChild 1—* LocalMatchReport               ← spelarkortet
```

**Viktiga affärsregler / invarianter:**
- En match måste ha lag, avsparkstid och motståndare för att kunna sparas.
- Avspark lagras i UTC; inmatning och visning sker i `Europe/Stockholm` (§KM.5).
- **Spelarkortet finns inte i databasen.** `LocalChild` och `LocalMatchReport` lever i enhetens egen
  lagring och har ingen motsvarighet på servern (§KM.2). Backend kan varken läsa eller ta emot dem.
- Mål och assist är aldrig negativa. Resultat får vara ofullständigt (bara ena laget ifyllt = ej sparat resultat).
- En inställd match visar ingen resultatinmatning och sätter `STATUS:CANCELLED` i ICS-feeden.
- `Player` på servern skapas **enbart av tränare** och används bara av den vilande kallelsen.
- `Attendance` får bara existera för lag där `Team.AttendanceEnabled` är sann.
- **Ett `CarpoolOffer` har 1–4 platser.** Endast **accepterade** förfrågningar förbrukar platser; en
  accept som skulle överskrida antalet avvisas server-side.
- **En `CarpoolRequest` kan skickas även när erbjudandet är fullt** — då nekar föraren med ett
  meddelande i stället för att den som frågar möts av en död knapp (§KM.12).
- **Ett nekande kräver alltid ett meddelande.** `CarpoolRequest.ResponseMessage` får inte vara tom när
  status sätts till `Declined`.
- Endast den som äger ett `CarpoolOffer` får besvara dess förfrågningar.
- Samåkningen för en match gallras 30 dagar efter matchen (§KM.12).

**Nyckelfält per entitet:**

| Entitet | Nyckelfält |
|---|---|
| `Team` | Id, AgeGroupId, Name, ColorHex, Slug, AttendanceEnabled |
| `Venue` | Id, Name, Address, Latitude, Longitude, IsHome |
| `Match` | Id, TeamId, KickoffUtc, OpponentName, VenueId, AddressOverride, IsHome, Status, Note, UpdatedByUserId, UpdatedUtc, IcsSequence |
| `Player` | Id, TeamId, FirstName, ShirtNumber, IsActive — *skapas endast av tränare, används av kallelsen* |
| `Guardian` | UserId, PlayerId, CreatedUtc |
| `User` | Id, Email, DisplayName, ConsentVersion, ConsentAcceptedUtc |
| `TeamRole` | UserId, TeamId, Role (Coach/Admin) |
| `Attendance` | Id, MatchId, PlayerId, Response, Note, AnsweredByUserId, AnsweredUtc |
| `CarpoolOffer` | Id, MatchId, UserId, Direction, Seats (1–4), DeparturePlace, DepartureUtc, Note, Status (Open/Full/Withdrawn) |
| `CarpoolRequest` | Id, OfferId, UserId, SeatsRequested, Greeting, Status (Pending/Accepted/Declined/Withdrawn), ResponseMessage, RespondedUtc |
| `PushSubscription` | Id, UserId, TeamId, Endpoint, P256dh, Auth, CreatedUtc, LastSeenUtc |
| `AuditLogEntry` | Id, ActorUserId, Action, EntityType, EntityId, BeforeJson, AfterJson, OccurredUtc, CorrelationId |
| *`LocalChild`* | *Endast på enheten:* Id, Namn, Tröjnummer, LagId |
| *`LocalMatchReport`* | *Endast på enheten:* MatchId, LocalChildId, VåraMål, DerasMål, Mål, Assist, Spelade |

## 7. Roller & behörigheter

| Roll | Får göra | Får INTE göra |
|------|----------|---------------|
| **Gäst** (ingen inloggning) | Se lagens matcher, matchdetalj, väder, vägbeskrivning, prenumerera på ICS och på notiser om matchändringar, **se lagets samåkningserbjudanden**, samt **föra sitt eget spelarkort på enheten** | Lägga upp samåkning, skicka åkförfrågan, se truppen, skriva något på servern |
| **Förälder** (inloggad) | Allt ovan + **lägga upp samåkning med 1–4 platser**, **skicka åkförfrågan**, **acceptera eller neka förfrågningar på sitt eget erbjudande**, hantera sina notiser, radera sitt konto | Svara på någon annans erbjudande, se truppen, ändra matcher |
| **Tränare** (per lag) | Allt ovan + skapa/ändra/ta bort/ställa in matcher i sitt lag, massinlägg, hantera truppen, se samåkningsöverblick, (när flaggan är på) kalla och se närvaro | Ändra andra lags matcher |
| **Admin** | Allt ovan för alla lag + skapa lag och säsong, ge och ta bort tränarroller, slå på `AttendanceEnabled` | — |

> **Notera:** spelarkortet saknas medvetet i tabellen ovan. Det ligger **enbart på familjens egen enhet**
> och passerar aldrig servern — därför finns det ingen roll som kan läsa det, inte ens Admin (§KM.2).
> Att föra spelarkortet kräver alltså inget konto alls.

## 8. API & integrationer
- **Externa tjänster:**
  - **Open-Meteo** (väder) — nyckelfri, anropas med fasta koordinater från vår egen `Venue`-tabell.
  - **E-postutskick** för inloggningskod (leverantör ej vald — se Öppna frågor).
  - **Web Push** via VAPID direkt mot webbläsarnas push-tjänster. Ingen tredjepartsleverantör.
  - **Kartappar** — endast utgående länk, ingen dataöverföring från oss.
- **Inkommande integrationer:** inga.
- **Datautbyte:** REST/JSON under `/api/v1/`. ICS som `text/calendar`.

**API-skiss:**

| Metod | Endpoint | Åtkomst |
|---|---|---|
| GET | `/api/v1/teams` | Anonym |
| GET | `/api/v1/teams/{slug}/matches` | Anonym |
| GET | `/api/v1/matches/{id}` | Anonym |
| GET | `/api/v1/matches/{id}/weather` | Anonym |
| GET | `/calendar/{teamSlug}.ics` | Anonym |
| POST/PUT/DELETE | `/api/v1/matches` · `/{id}` | Coach (eget lag) |
| POST | `/api/v1/matches/bulk-preview` · `/bulk-import` | Coach |
| GET/POST/DELETE | `/api/v1/players` · `/{id}` | Coach |
| GET | `/api/v1/matches/{id}/carpool` | Anonym |
| POST/DELETE | `/api/v1/matches/{id}/carpool` · `/offers/{id}` | Inloggad |
| POST/DELETE | `/api/v1/carpool/offers/{id}/requests` · `/requests/{id}` | Inloggad |
| POST | `/api/v1/carpool/requests/{id}/accept` | Erbjudandets ägare |
| POST | `/api/v1/carpool/requests/{id}/decline` | Erbjudandets ägare — **meddelande krävs** |
| GET/PUT | `/api/v1/matches/{id}/attendance` | Coach / Guardian, **404 om flaggan är av** |
| POST/DELETE | `/api/v1/push/subscriptions` | Anonym (matchändringar) / inloggad (samåkning) |
| GET | `/health` · `/health/ready` | Anonym |

> **Det finns ingen endpoint för spelarstatistik — och får aldrig införas** utan ett skrivet beslut
> i handoff-filen. Ett arkitekturtest ska verifiera detta (§KM.2).

## 9. UI — sidor & flöden
- **Huvudsidor:** Lagväljare · Lagets schema (nästa match, kommande, tidigare) · Matchdetalj ·
  Mitt barn (spelarkort) · Samåkning för en match · Logga in · Inställningar (notiser, kalender, konto,
  radera) · Tränarvy (matchlista, redigera, massinlägg, trupp) · Adminvy (lag, roller, flaggor).
- **Kritiska flöden:**
  1. Öppna länk → välj lag → se nästa match → vägbeskrivning. **Utan konto, under tio sekunder.**
  2. Prenumerera på kalendern → matcherna finns i telefonens kalender för alltid.
  3. Tränare loggar in → klistra in schema → granska → spara → föräldrar får notis.
  4. Förälder lägger till sitt barn på enheten → fyller i resultat och mål tillsammans med barnet efter
     matchen → ett märke låses upp. **Inget konto, inget nätverksanrop.**
  5. Förälder loggar in → lägger upp "plats för 3" till bortamatchen → en annan förälder skickar en
     förfrågan → föraren accepterar, eller nekar med ett meddelande → båda får notis.
- **Designkrav:** Bygger vidare på den befintliga appens identitet — lagfärgen som tema (Gul `#D9A21B`,
  Blå `#1E3F8A`, Vit `#D9D9D9`, Svart `#161616`), Barlow och Barlow Condensed, kortbaserad layout.
  Mobil först, ljust och mörkt läge, stora träffytor, WCAG 2.1 AA.

## 10. Icke-funktionella krav
- **Prestanda:** Schemat för ett lag laddar under 500 ms på 4G. Appskalet är interaktivt under 2 s på en fem år gammal telefon. ICS-feeden svarar under 300 ms och är cachad.
- **Säkerhet:** Baslinjen i `CLAUDE.md` plus §KM. Spelarkortet lagras enbart på enheten och passerar aldrig servern (§KM.2). Rate limiting på publika endpoints från start.
- **GDPR/PII:**
  - **Personuppgifter vi behandlar på servern:** vårdnadshavarens e-post och visningsnamn; push-endpoint (enhetsidentifierare); fritext i samåkning; audit-logg med användar-id. Dessutom barnets förnamn och tröjnummer **först när en tränare lägger upp truppen** för den vilande kallelsen.
  - **Personuppgifter vi INTE behandlar:** spelarkortet — barnets namn, resultat, mål och assist — stannar på familjens egen enhet och når aldrig servern (§KM.2). Vid lansering, med kallelsen avstängd, behandlar servern alltså **inga uppgifter alls om barn**.
  - **Laglig grund:** berättigat intresse/avtal för kontot och samåkningen; samtycke från vårdnadshavare den dag truppen läggs upp.
  - **Gallring:** push-prenumerationer rensas när de blir ogiltiga eller efter 12 månaders inaktivitet. **Samåkning gallras 30 dagar efter matchen** (§KM.12). Matchdata gallras efter avslutad säsong + 2 år, eller vid begäran.
  - **Radering:** i appen, direkt, av både konto och trupp (§KM.6). Spelarkortet raderas separat på enheten och kan raderas när som helst utan att någon behöver kontaktas.
  - **Ingen data lämnar EU.** Ingen spårning, ingen besöksanalys.
- **Tillgänglighet:** WCAG 2.1 AA som eget krav (§KM.0 A3).
- **Skalbarhet/drift:** ~130 användare, ~200 matcher per säsong. En instans räcker med marginal.
  Frontend på Vercel, backend som Docker-container på Render, databas på Neon — allt på fria nivåer (§KM.11).
  Målsatt upptid: appen ska fungera på lördagsmorgnar — planerat underhåll läggs aldrig fredag–söndag under säsong.
  **Kallstart:** Render free somnar efter ca 15 minuters tystnad. Publika sidvisningar ska därför besvaras
  av Vercels edge-cache utan att backend väcks; det är den vanligaste sidvisningen i appen.
- **Webbläsare/enheter:** Senaste två versionerna av Safari (iOS), Chrome (Android), Edge och Firefox. Mobil först, men läsbar på surfplatta och dator.

## 11. Vilka "vid behov"-element gäller?

| Element | Aktuellt? | Trigger / motivering |
|---------|:---------:|----------------------|
| Rate limiting | **J — lyft till baslinje** | Publika oautentiserade endpoints på öppet internet (§KM.0 A1) |
| DB-backup & testad återställning | **J — lyft till baslinje** | Ett tappat matchschema mitt i säsongen kostar fyra tränare en kväll var (§KM.0 A2) |
| Caching | **J** | Publika schema- och ICS-endpoints läses ofta och ändras sällan → output-cache + ETag. Dessutom **edge-cache på Vercel**, som döljer Renders kallstart för den vanligaste sidvisningen (§KM.11) |
| Background jobs | **J** | Push-utskick och påminnelse dagen före får aldrig ske i request-tråden |
| CI/CD & deployment | **J** | Ska driftsättas — inför tidigt |
| CSRF-skydd | **J** | Refresh-token i `httpOnly`-cookie |
| Account lockout | **J** | Egen inloggning med e-postkod |
| Global state (FE) | **J (lätt)** | Inloggad användare och valt lag delas → Context |
| SAST/DAST | **J före lansering** | Publik app med personuppgifter |
| **Beständig lagring i klienten** | **J — ny, kritisk** | Spelarkortet finns bara på enheten. `navigator.storage.persist()`, säkerhetskopieringskod och tydlig uppmaning att installera på hemskärmen (§KM.2) |
| Pagination | **N (bevakas)** | ~25–50 matcher per lag och säsong. Införs när flera säsonger ackumulerats |
| Offline-först med synkkö | **N** | v1 är offline-medveten (§KM.8). Införs bara om skrivning offline visar sig behövas |
| i18n | **N** | Endast svenska |
| Fältkryptering i vila | **N** | Servern lagrar ingen känslig PII — barnstatistiken finns inte där alls (§KM.2) |
| Redis / distribuerad cache | **N** | En instans, ingen mätbar flaskhals |
| Certificate pinning | **N** | Ej tillämpligt för webb-PWA |

## 12. Definition of Done & acceptans
- **Per feature:** checklistan i [`CLAUDE.md`](./CLAUDE.md).
- **Per release:** [`SAKERHET-CHECKLISTA.md`](./SAKERHET-CHECKLISTA.md) — alla baslinjerader `✅` eller `➖` med motivering.
- **Leveransklart (v1) när:** en förälder kan öppna länken utan konto, se lagets matcher med väder och
  vägbeskrivning, prenumerera på kalendern, föra sitt barns spelarkort på egen enhet och säkerhetskopiera
  det, logga in och lägga upp eller begära samåkning där föraren själv accepterar eller nekar med ett
  meddelande, och få notis när en match ändras — samtidigt som en tränare kan lägga in hela säsongens
  schema, ändra det och ställa in en match. Allt med tester gröna och säkerhetschecklistan avbockad.

## 13. Antaganden & öppna frågor

**Antaganden:**
- Fyra lag i P2016 under säsong 2026; strukturen `Club → AgeGroup → Team` är förberedd för fler.
- Ingen betalning, ingen kommersiell användning — Vercel Hobby, Neon Free och motsvarande fria nivåer är tillåtna.
- Matchtiderna kommer manuellt från tränarna. Ingen automatisk import från förbundets system i v1.
- Föräldrarna har smartphones med moderna webbläsare.

**Öppna frågor:**
1. **Har klubben redan ett officiellt verktyg** (laget.se, Svenskalag eller liknande)? Avgör Filter 4 och måste besvaras innan M3 (tränaradmin).
2. **Vill tränarna använda den?** Förankring krävs innan tränardelen byggs.
3. ~~**Var driftas backend?**~~ **Besvarad 2026-08-29:** Vercel (frontend) + Render (backend som Docker) + Neon (databas), med `/api/*`-rewrite i `vercel.json` — samma uppsättning som carcheck.se. Se §KM.11.
4. **Vilken e-postleverantör** för inloggningskoden? Behöver klara EU-hosting och ha en gratisnivå.
5. **Domännamn** — `karramatcher.se` föreslaget, tillgänglighet ej kontrollerad.
6. **Vem är admin och vem är tränare?** Rollerna behöver riktiga personer innan lansering.
7. **Samtyckestexten** behöver formuleras — men blev mindre brådskande i och med beslutet att hålla
   spelarkortet på enheten: vid lansering behandlar servern inga uppgifter om barn alls. Samtycket
   behövs först den dag en tränare lägger upp truppen för kallelsen. Kvarstår: en begriplig
   integritetstext som förklarar att spelarkortet bor i telefonen och försvinner utan säkerhetskopia.
8. **Hur hittar föräldrar varandra vid samåkning?** Vi lagrar inga telefonnummer (§KM.1). Räcker
   namn plus meddelandefältet, eller behövs något mer? Prövas med tränarna innan M5.

## 14. Milstolpar & leverans

Se [`docs/MVP-PLAN.md`](./docs/MVP-PLAN.md) för innehåll per milstolpe.

| Milstolpe | Innehåll |
|-----------|----------|
| **M0** Grund | Repo, board, .NET-lösning, databas, CI, Dockerfile, rewrite, seed, testad återställning |
| **M1** Publika delen | Lagsidor, matchlista, matchdetalj, väder, karta, ICS, PWA — helt utan konto |
| **M2** Konto & roller | Inloggning med e-postkod, JWT, refresh-rotation, tränar- och adminroller |
| **M3** Tränaradmin | CRUD på matcher, massinlägg, trupp |
| **M4** Spelarkortet | Helt på enheten: barn, matchrapport, märken, säkerhetskopiering, import |
| **M5** Samåkning | Erbjud 1–4 platser, skicka förfrågan, acceptera eller neka med meddelande |
| **M6** Kallelse (avstängd) | Byggd bakom `AttendanceEnabled` |
| **M7** Notiser | Web Push vid matchändring och samåkningshändelser, påminnelse dagen före |
| **M8** Lansering | Integritetstext, a11y-genomgång, säkerhetschecklista, domän, utrullning |

---

> **Till agenten:** Börja med en kort plan baserad på denna spec. Bygg sedan **en vertical slice i taget**
> (Must-features först) enligt `CLAUDE.md`, med tester per steg. Föreslå "vid behov"-element när deras
> trigger uppfylls — bygg inte in dem i förväg.
