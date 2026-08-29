# STANDARDER VID BEHOV — element att införa när kravet finns (Kärra Matcher)

> **Till agenten:** Dessa element är **inte** obligatoriska från start. Känn till dem,
> men inför dem **bara när triggern uppfylls** — annars blir det over-engineering (YAGNI).
> När du inför ett element: följ samma mönster och kvalitetsnivå som i [`CLAUDE.md`](./CLAUDE.md).
> Föreslå för användaren när du ser att en trigger är uppfylld, i stället för att bygga in det tyst.
>
> **Ursprung:** anpassad från `mallar/StrukturBackendFrontend/STANDARDER-VID-BEHOV.md`.
> Mobil-/native-element är borttagna; webb- och PWA-element är tillagda.

Varje element har: **Vad** · **Inför NÄR (trigger)** · **Kort hur**.

---

## ⚡ Redan utlösta i detta projekt

Följande element har **redan** fått sin trigger uppfylld enligt [`SPEC.md`](./SPEC.md) §11 och ska
byggas i den milstolpe där de hör hemma — de är alltså inte längre valfria:

| Element | Utlöst av | Milstolpe |
|---|---|---|
| **Rate limiting** *(lyft till baslinje, §KM.0 A1)* | Publika oautentiserade endpoints på öppet internet | M0–M1 |
| **DB-backup & testad återställning** *(lyft till baslinje, §KM.0 A2)* | Familjestatistik som inte kan återskapas | M0 |
| **CI/CD & deployment** | Projektet ska driftsättas | M0 |
| **Caching (output-cache + ETag)** | Publika schema- och ICS-endpoints läses ofta, ändras sällan | M1 |
| **Background jobs** | Push-utskick och påminnelse dagen före | M6 |
| **CSRF-skydd** | Refresh-token i `httpOnly`-cookie | M3 |
| **Account lockout** | Egen inloggning med e-postkod | M3 |
| **Global state (Context)** | Inloggad användare och valt lag delas i FE | M1 |
| **SAST/DAST** | Publik app med barns personuppgifter | M7 |

Resten av dokumentet är fortfarande vilande.

---

## 🔵 BACKEND — vid behov

### Pagination, filtrering & sortering
- **Vad:** Sidindela list-endpoints (`PagedResult`), filtrera/sortera på `IQueryable`.
- **Inför NÄR:** en lista kan returnera fler än ~några tiotal rader. *(Här: ca 25–50 matcher per lag och säsong — trycket uppstår först när flera säsonger ackumulerats.)*
- **Hur:** `Skip/Take` i databasen, begränsa max `PageSize`, returnera `totalCount`. Cursor-baserat om oändlig scroll införs.

### Distribuerad cache / Redis
- **Vad:** Delad cache mellan instanser.
- **Inför NÄR:** backend skalas till fler än en instans. *(In-memory output-cache räcker på en instans.)*
- **Hur:** Cache-aside med TTL och tydlig invalidation. Cacha aldrig användarspecifik data oavsiktligt.

### Domain events / integration events
- **Vad:** Entiteter publicerar domänhändelser; sidoeffekter kopplas löst.
- **Inför NÄR:** flera sidoeffekter ska ske vid samma domänändring — t.ex. när en match flyttas ska både push skickas, ICS-sekvensen ökas och audit-loggen skrivas.
- **Hur:** Domain events i Domain, hanteras i Application. Outbox-mönster om händelserna måste vara tillförlitliga. *(Trolig kandidat vid M6 — bevaka.)*

### Read models / separat läsmodell
- **Vad:** Optimerade läsvyer skilda från skrivmodellen.
- **Inför NÄR:** läs- och skrivbehoven skiljer sig markant och prestanda kräver det.
- **Hur:** Egna query-modeller. Inför **inte** i förväg — komplext och inte motiverat i den här datamängden.

### Distribuerad tracing & metrics (OpenTelemetry)
- **Vad:** End-to-end-spårning och mätvärden.
- **Inför NÄR:** systemet växer till flera tjänster, eller latens måste felsökas över tjänstegränser.
- **Hur:** OpenTelemetry → valfri backend. Bygger på correlation-ID från baslinjen.

### Automatisk schemaimport
- **Vad:** Hämta matchschemat direkt från förbundets eller seriearrangörens system i stället för manuell inmatning.
- **Inför NÄR:** massinlägget visar sig otillräckligt, eller en användbar källa identifieras.
- **Hur:** Schemalagt jobb som hämtar, jämför mot befintliga matcher och föreslår ändringar för tränaren att godkänna — aldrig tyst överskrivning av handinmatad data.

### Central pakethantering & feature flags
- **Vad:** `Directory.Packages.props`; feature flags i drift.
- **Inför NÄR:** fler projekt delar beroenden; eller funktioner behöver släppas gradvis.
- **Hur:** Central Package Management samlar versioner. *(Notera: `Team.AttendanceEnabled` är en enkel datadriven flagga och kräver inget ramverk — se §KM.7.)*

---

## 🟢 FRONTEND — vid behov

### Offline-först med synkkö
- **Vad:** Skrivningar köas lokalt när nätet är borta och skickas när det kommer tillbaka.
- **Inför NÄR:** användare faktiskt försöker fylla i resultat utan täckning och tappar data. *(v1 är offline-**medveten** enligt §KM.8 — läsning fungerar, skrivning kräver nät och säger det tydligt.)*
- **Hur:** TanStack Query-persistens eller IndexedDB + en utgående mutations-kö, konfliktstrategi (senast skrivna vinner per fält) och synlig synk-status. Komplext — bygg inte på spekulation.

### Zustand eller annat state-bibliotek
- **Vad:** Dynamiskt globalt client-state utöver Context.
- **Inför NÄR:** Context börjar orsaka onödiga omrenderingar som du **mätt**.
- **Hur:** Zustand för dynamiskt state. Server-state hör hemma i TanStack Query, inte här.

### Performance & optimering
- **Vad:** `React.memo`, `useMemo`, `useCallback`, code-splitting, list-virtualisering.
- **Inför NÄR:** du har **mätt** ett verkligt problem (Profiler eller Lighthouse).
- **Hur:** Memoisera dyra komponenter, splittra på route, virtualisera långa listor. Optimera aldrig i förväg.

### Internationalisering (i18n)
- **Vad:** Flerspråksstöd.
- **Inför NÄR:** appen ska finnas på fler språk än svenska. *(Tänkbart om klubben får familjer som hellre läser engelska — men inte förrän någon frågar.)*
- **Hur:** Inga hårdkodade strängar — allt via nycklar. Dyrt att eftermontera, så besluta medvetet.

### Avancerad formulärhantering
- **Vad:** Flerstegsformulär, dynamiska fält-arrayer.
- **Inför NÄR:** formulären blir komplexa bortom enkel single-step. *(Massinlägget är kandidat om det växer bortom klistra in → granska → spara.)*
- **Hur:** React Hook Form `useFieldArray`, Zod-validering per steg.

### Delbar matchbild (Canvas/OG-bild)
- **Vad:** Generera en bild av matchresultatet att dela i gruppchatten.
- **Inför NÄR:** föräldrar ber om det, eller dela-funktionen visar sig användas flitigt.
- **Hur:** Rendera server-side eller på Canvas i klienten. **Får aldrig innehålla andra barns namn** (§KM.1).

---

## 🔒 SÄKERHET — vid behov (utöver baslinjen i CLAUDE.md)

> Baslinjen (AuthN/AuthZ, objektnivå-auktorisering, secrets, input-validering, CORS/HTTPS, CSP,
> rate limiting, audit, GDPR-grund, barn-PII, DB-backup, dependency-scan) ligger i `CLAUDE.md`
> och gäller alltid. Nedan är fördjupningar.

### Fältkryptering i vila (PII)
- **Vad:** Kryptera specifika känsliga kolumner.
- **Inför NÄR:** särskilt känsliga personuppgifter börjar lagras. *(Utlöses automatiskt om §KM.1 någon gång luckras upp — vilket kräver ett beslut i handoff-filen.)*
- **Hur:** Kryptering på applikationsnivå, nycklar i secret store med rotation.

### Multifaktor-autentisering
- **Vad:** Andra faktor utöver e-postkoden.
- **Inför NÄR:** tränar- eller admin-konton bedöms behöva starkare skydd — de kan ändra schemat för hela laget.
- **Hur:** TOTP eller passkeys. Överväg att kräva det enbart för Admin-rollen.

### Secrets-rotation
- **Vad:** Rotera nycklar utan nedtid.
- **Inför NÄR:** produktion med långlivade hemligheter. *(VAPID-nycklarna är kandidat — byte tvingar om alla push-prenumerationer, så planera det.)*
- **Hur:** Versionerade secrets, flera giltiga nycklar under övergång.

### Penetrationstest & extern säkerhetsgranskning
- **Vad:** Extern granskning av app och backend.
- **Inför NÄR:** klubben eller förbundet ställer krav, eller om appen breddas till fler åldersgrupper och användarantalet växer väsentligt.
- **Hur:** Beställ extern pen-test, åtgärda fynd, dokumentera.

### Subresource Integrity & striktare CSP
- **Vad:** SRI-hashar på externa resurser; CSP utan `unsafe-*`.
- **Inför NÄR:** externa skript eller stilar någon gång läggs till. *(§KM.6 säger att inga tredjeparter får läggas till utan beslut — så triggern bör i praktiken aldrig lösas ut.)*
- **Hur:** `integrity`-attribut, nonce-baserad CSP.

---

## 🧭 Snabb beslutsregel för agenten

> **"Behöver detta projekt elementet *nu*, eller löser jag ett problem jag inte har än?"**
> Är svaret det senare → nämn elementet för användaren och vänta. Bygg inte in det i förväg.
