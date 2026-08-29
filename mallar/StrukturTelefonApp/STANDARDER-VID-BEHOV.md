# STANDARDER VID BEHOV — element att införa när kravet finns (telefonapp)

> **Till agenten:** Dessa element är **inte** obligatoriska från start. Känn till dem,
> men inför dem **bara när triggern uppfylls** — annars blir det over-engineering (YAGNI).
> När du inför ett element: följ samma mönster och kvalitetsnivå som i [`CLAUDE.md`](./CLAUDE.md).
> Föreslå för användaren när du ser att en trigger är uppfylld, istället för att bygga in det tyst.

Varje element nedan har: **Vad** · **Inför NÄR (trigger)** · **Kort hur**.

---

## 🔵 BACKEND — vid behov

### Pagination, filtrering & sortering
- **Vad:** Sidindela list-endpoints (`PagedResult`), filtrera/sortera på `IQueryable`.
- **Inför NÄR:** så fort en lista kan returnera fler än ~några tiotal rader. *(För mobil i praktiken nästan alltid — nät och minne är begränsat.)*
- **Hur:** `Skip/Take` i databasen, begränsa max `PageSize`, returnera `totalCount`. Överväg cursor-baserad pagination för oändliga listor (infinite scroll i appen).

### Caching
- **Vad:** `IMemoryCache` eller distribuerad cache / **Redis**.
- **Inför NÄR:** en endpoint är mätbart långsam, läses ofta, ändras sällan; eller appen skalas till fler än en instans.
- **Hur:** Cache-aside med TTL + tydlig invalidation. Cacha aldrig känslig/användarspecifik data oavsiktligt.

### Background jobs & resiliens
- **Vad:** Bakgrundsjobb (Hangfire / `IHostedService`) + retry/circuit breaker (Polly).
- **Inför NÄR:** tunga uppgifter (mejl, rapporter, import), schemalagt, **push-notiser i mängd**, eller externa anrop som kan fela.
- **Hur:** Flytta tungt arbete ur request-tråden. Idempotenta jobb. Exponential backoff + circuit breaker + timeouts.

### Push-notis-tjänst (utbyggd)
- **Vad:** Robust leverans till APNs/FCM (eller Expo Push), segmentering, kvitton, avregistrering av döda tokens.
- **Inför NÄR:** notiser blir en kärnfunktion (inte bara enstaka transaktionsnotis).
- **Hur:** Köa via bakgrundsjobb, hantera leveranskvitton, rensa ogiltiga tokens, respektera användarens notis-inställningar.

### CI/CD, Docker & deployment (BE)
- **Vad:** Containerisering + pipeline (build → test → scan → deploy).
- **Inför NÄR:** så fort backend ska driftsättas/delas. *(Inför tidigt.)*
- **Hur:** Multi-stage Dockerfile, CI som faller vid testfel, separata miljöer (Staging→Prod), kontrollerade DB-migrationer, versionstaggning + rollback.

### Domain events / integration events
- **Vad:** Entiteter publicerar domänhändelser; integration events mellan moduler.
- **Inför NÄR:** sidoeffekter vid domänändring utan hård koppling (t.ex. "skicka push när order bekräftas").
- **Hur:** Domain events i Domain, hanteras i Application. Outbox-mönster om händelser måste vara tillförlitliga.

### Rate limiting / throttling
- **Vad:** Begränsa requests per klient/IP (ASP.NET inbyggda rate limiter).
- **Inför NÄR:** publika/oautentiserade endpoints, inloggning, eller API som kan missbrukas.
- **Hur:** Fixed/sliding window eller token bucket. Returnera `429` med `Retry-After`. Kombinera med account lockout.

### Distribuerad tracing & metrics (OpenTelemetry)
- **Vad:** End-to-end-spårning och mätvärden.
- **Inför NÄR:** systemet växer till flera tjänster, eller du behöver felsöka latens över tjänstegränser.
- **Hur:** OpenTelemetry → Jaeger/Prometheus/Grafana. Bygger på correlation-ID från baslinjen.

### DB-backup & katastrofåterställning (DR)
- **Vad:** Regelbunden, testad säkerhetskopiering av databasen + plan för återställning.
- **Inför NÄR:** så fort riktig användardata finns i produktion. *(Inför tidigt — dataförlust är oåterkalleligt.)*
- **Hur:** Automatiska backuper med definierad RPO/RTO, point-in-time recovery om möjligt, **testa återställning** (en otestad backup är ingen backup), kryptera backuper, geografisk redundans vid behov.

---

## 🟢 FRONTEND / APPEN — vid behov

### Offline-först & lokal synk
- **Vad:** Appen fungerar utan nät; data köas lokalt och synkas vid återkomst.
- **Inför NÄR:** appen ska användas i miljöer med opålitligt nät, eller offline-användning är ett produktkrav. *(Baslinjens "offline-medvetenhet" räcker för många appar — full synk är komplext.)*
- **Hur:** Lokal lagring (SQLite via `expo-sqlite`/WatermelonDB eller TanStack Query-persistens), en utgående mutations-kö, konfliktstrategi (last-write-wins eller per-fält), tydlig synk-status i UI.

### Global state (Context / Zustand)
- **Vad:** Delad client-state (inloggad användare, tema).
- **Inför NÄR:** samma client-state behövs av komponenter på olika ställen i trädet.
- **Hur:** Context för sällan-ändrad data (memoisera värdet), Zustand för dynamiskt state. Server-state hör hemma i TanStack Query.

### Performance & optimering
- **Vad:** `React.memo`, `useMemo`, `useCallback`, list-optimering, Hermes-profilering, bild-caching.
- **Inför NÄR:** du har **mätt** ett verkligt problem — onödiga omrenderingar, hackiga listor, långsam start.
- **Hur:** Memoisera dyra komponenter/beräkningar, optimera `FlatList` (`getItemLayout`, `windowSize`), `expo-image`-caching. Optimera aldrig i förväg.

### Internationalisering (i18n)
- **Vad:** Flerspråksstöd (t.ex. `i18next` / `expo-localization`).
- **Inför NÄR:** appen ska finnas på fler än ett språk, eller kunden kräver det.
- **Hur:** Inga hårdkodade strängar — allt via nycklar. Hantera enhetens språk/locale. Inför tidigt om känt krav (dyrt att eftermontera).

### Avancerad formulärhantering
- **Vad:** Multi-step wizards, dynamiska fält-arrayer, beroende fält.
- **Inför NÄR:** formulären blir komplexa bortom enkel single-step.
- **Hur:** Bygg vidare på React Hook Form (`useFieldArray`), behåll Zod-validering per steg.

### Native-funktioner (kamera, plats, fil, betalning)
- **Vad:** Enhetshårdvara via Expo-moduler (`expo-camera`, `expo-location`, `expo-image-picker`, in-app purchases m.m.).
- **Inför NÄR:** en feature faktiskt kräver hårdvaran.
- **Hur:** Begär behörighet just-in-time med förklaring, hantera nekad behörighet, testa på fysisk enhet (inte bara emulator). In-app purchases måste följa Apples/Googles regler om de säljer digitalt innehåll.

---

## 🔒 SÄKERHET — vid behov (utöver baslinjen i CLAUDE.md)

### Certificate pinning
- **Vad:** Appen litar bara på din servers specifika certifikat/nyckel.
- **Inför NÄR:** känslig app (finans, hälsa) eller krav på skydd mot man-in-the-middle.
- **Hur:** Pinna publik nyckel i nätverkslagret. Ha en rotationsplan så pinning inte låser ute appen vid certbyte.

### Root-/jailbreak-detektering & anti-tampering
- **Vad:** Upptäck om appen körs på en rootad/jailbreakad enhet eller har manipulerats; försvåra reverse engineering.
- **Inför NÄR:** finans-/hälsoapp, betalningar, eller höga krav på integritet i klienten.
- **Hur:** Detektering (t.ex. via känt bibliotek), kodobfuskering, integritetskontroll av binären. Behandla klienten som icke betrodd ändå — verifiera allt server-side. Detektering höjer ribban men ersätter inte serverskydd.

### Multifaktor-autentisering (MFA / 2FA)
- **Vad:** Andra faktor utöver lösenord — TOTP-app, SMS, e-post, eller passkeys.
- **Inför NÄR:** känsliga konton, kundkrav, eller höjd kontosäkerhet behövs.
- **Hur:** Bygg på MFA-beredskapen i `CLAUDE.md`. Föredra TOTP/passkeys framför SMS. Hantera backupkoder och återställning säkert.

### WebView-härdning
- **Vad:** Säker inbäddning av webbinnehåll (`react-native-webview`).
- **Inför NÄR:** appen måste visa extern eller egen webb i en WebView.
- **Hur:** Ladda bara betrodda HTTPS-URL:er, stäng av onödig JS-bridge, exponera aldrig native-funktioner till otillförlitligt innehåll, validera all postMessage-data. Undvik WebView för känsliga inloggningsflöden.

### Fältkryptering i vila (PII)
- **Vad:** Kryptera specifika känsliga kolumner i databasen.
- **Inför NÄR:** du lagrar särskilt känsliga personuppgifter (personnummer, hälsodata) eller regelverk kräver det.
- **Hur:** Kryptera på applikationsnivå eller via DB-funktion; nycklar i Key Vault, med rotation.

### Account lockout / brute-force-skydd
- **Vad:** Lås konto / fördröj efter upprepade misslyckade inloggningar.
- **Inför NÄR:** egen inloggning (inte enbart extern IdP).
- **Hur:** Räkna misslyckanden, exponentiell fördröjning/lockout, kombinera med rate limiting.

### SAST/DAST & säkerhetstestning i pipeline
- **Vad:** Statisk och dynamisk säkerhetsanalys.
- **Inför NÄR:** inför produktionssläpp / kundkrav på säkerhetsrevision.
- **Hur:** CI (CodeQL, OWASP ZAP, samt mobil-scanner som MobSF för appbinären). Åtgärda fynd före release.

### Secrets-rotation
- **Vad:** Rotera nycklar/lösenord regelbundet utan nedtid.
- **Inför NÄR:** produktion med långlivade hemligheter och säkerhetskrav.
- **Hur:** Key Vault med versionerade secrets, flera giltiga nycklar under övergång.

### Penetrationstest & säkerhetsgranskning
- **Vad:** Extern säkerhetsgranskning av app + backend.
- **Inför NÄR:** inför kundleverans eller vid känd känslig data.
- **Hur:** Beställ extern pen-test, åtgärda fynd, dokumentera för kundens revision.

---

## 🧭 Snabb beslutsregel för agenten

> **"Behöver detta projekt elementet *nu*, eller löser jag ett problem jag inte har än?"**
> Är svaret det senare → nämn elementet för användaren och vänta. Bygg inte in det i förväg.
