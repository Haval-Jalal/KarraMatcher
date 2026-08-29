# Driftövervakning

> Hur vi vet att backend lever, och hur vi ser till att den är vaken när föräldrarna
> faktiskt öppnar appen.
>
> Regelverk: [`CLAUDE.md`](../CLAUDE.md) §KM.11 · Säkerhetschecklistan rad 11.8

---

## Problemet

Render free stänger av tjänsten efter **15 minuter utan trafik**, och den tar ungefär en
minut att vakna. Appen används mest lördag morgon, efter en tyst natt — den första
föräldern som kollar avsparkstiden skulle alltså få vänta.

Den självklara lösningen är att pinga tjänsten var femte minut dygnet runt. **Det är en fälla.**

### Timbudgeten som gör det till en fälla

Render ger **750 fria instanstimmar per kalendermånad och arbetsyta**. Tar de slut
suspenderas *alla* fria tjänster till den första i nästa månad.

En månad med 31 dagar är 744 timmar. En ping dygnet runt håller tjänsten vaken hela tiden:

| Strategi | Timmar per månad | Marginal till 750 |
|---|---|---|
| Ping var 5:e minut, dygnet runt | **744** (31-dagarsmånad) | **6 timmar** |
| Fönstrad ping (vår lösning) | **~368** | **~382 timmar** |

Sex timmars marginal betyder att en enda extra fri tjänst, en serie omdeployer eller en
trafiktopp kan släcka hela appen i upp till trettio dagar. Det är ett dåligt byte för en
app som hundra familjer förlitar sig på.

---

## Lösningen: pinga när folk faktiskt är där

Tjänsten hålls vaken när appen används, och tillåts sova när den inte gör det.

| När | Intervall | Varför |
|---|---|---|
| **Fredag 15:00 – söndag 23:59** | var 5:e minut | Matchhelgen. Ingen förälder ska möta en kallstart lördag morgon |
| **Övrig tid** | varje timme | Räcker för att upptäcka avbrott. Tjänsten är vaken ~15 min per timme |

**Räkningen:** 57 timmar i fönstret plus 111 timmar × 25 % utanför = ~85 timmar i veckan,
alltså **~368 timmar i månaden**. Marginalen till taket är ~382 timmar.

### Vad det kostar

En tränare som redigerar schemat en tisdagskväll kan få vänta ungefär en minut på att
tjänsten vaknar. Det är **en person som gör en planerad sak**, inte hundra föräldrar som
snabbt vill se en matchtid. UI:t ska säga ifrån på svenska vid långsamt svar (§KM.11), inte
visa en spinner som ser trasig ut.

Utanför fönstret upptäcks ett avbrott inom en timme i stället för inom fem minuter. Under
helgen, när det spelar roll, är det fortfarande fem minuter.

### Edge-cachen bär det vanliga fallet

Publika GET-svar cachas på Vercels edge (`#13`), så lagets schema och ICS-feeden kan besvaras
**utan att Render väcks alls**. Övervakningen är därför ett komplement, inte det enda som står
mellan en förälder och en kallstart.

---

## Verktyget

**[cron-job.org](https://cron-job.org)** — fri nivå, upp till en körning per minut, egen
schemaläggning per veckodag och timme, och avisering både när ett jobb börjar fallera och när
det fungerar igen. EU-baserad tjänst.

### Varför inte GitHub Actions

En schemalagd workflow hade varit gratis och legat i repot. Två saker gör den olämplig:
GitHub **stänger av schemalagda workflows efter 60 dagars inaktivitet i repot**, vilket är en
realistisk risk för en säsongsapp som ligger stilla över vintern. Och GitHubs cron är
best-effort — körningar försenas eller hoppas över vid belastning, vilket är precis fel
egenskap för något som ska hålla en tjänst vaken.

---

## Sätta upp

Fyra jobb. Alla pekar på `/health` — **aldrig** på en tung endpoint, och aldrig på
`/health/ready`, som går ner i databasen och skulle belasta Neon i onödan.

Adressen är backendens Render-URL direkt, inte Vercel-domänen. `/health` ligger utanför
`/api`-rewriten och proxas inte.

### Jobb 1–3: matchhelgen, var 5:e minut

cron-job.org schemalägger som en korsprodukt av veckodagar och timmar, så helgfönstret
behöver tre jobb för att få rätt start- och sluttid:

| Jobb | Veckodag | Timmar | Minuter |
|---|---|---|---|
| `karra-halsa-fredag` | Fredag | 15–23 | var 5:e |
| `karra-halsa-lordag` | Lördag | 0–23 | var 5:e |
| `karra-halsa-sondag` | Söndag | 0–22 | var 5:e |

### Jobb 4: övrig tid, varje timme

| Jobb | Veckodag | Timmar | Minuter |
|---|---|---|---|
| `karra-halsa-baslinje` | Alla | 0–23 | `0` |

Det överlappar helgjobben, vilket är ofarligt — tjänsten är redan vaken då.

### Steg för steg

1. Skapa konto på [cron-job.org](https://cron-job.org) och verifiera e-postadressen.
   **Använd en adress du faktiskt läser** — det är den som larmet går till.
2. **Create cronjob**.
3. **Title:** enligt tabellen ovan. **URL:** backendens `/health`.
4. Under **Schedule**, välj **Custom** och kryssa i veckodagar, timmar och minuter enligt
   tabellen.
5. Under **Notifications**, slå på avisering vid **failure** och vid **success after
   failure**. Det andra är lika viktigt — utan det vet du inte när något är löst.
6. Sätt **Request timeout** till minst **60 sekunder**. En tjänst som håller på att vakna tar
   ungefär en minut, och en kortare timeout ger falsklarm varje gång den startar.
7. Spara. Upprepa för alla fyra jobben.

> **Timeouten är inte en detalj.** Med standardvärdet larmar baslinjejobbet varje gång
> tjänsten vaknar, och larm som ljuger slutar man läsa.

---

## Verifiera att larmet fungerar

Ett larm som aldrig prövats är en förhoppning, inte ett larm.

1. Gå till Renders dashboard → tjänsten → **Suspend Service**.
2. Vänta tills nästa körning av `karra-halsa-baslinje` (som mest en timme). Under helgen går
   det på fem minuter.
3. **Kontrollera att mejlet faktiskt kom fram** — inklusive skräpposten. Kom det inte fram är
   larmkanalen inte verifierad, oavsett vad cron-job.org visar i sitt gränssnitt.
4. **Resume Service** i Render.
5. Kontrollera att återhämtningsmejlet kommer.
6. Skriv in datum och resultat i tabellen längst ned.

---

## Utanför säsong

Fotbollssäsongen sträcker sig ungefär april till oktober. Under vintern finns inga matcher,
och helgfönstret fyller ingen funktion.

**Pausa jobb 1–3** i cron-job.org när säsongen är slut. Låt `karra-halsa-baslinje` gå — den
kostar ~120 timmar i månaden och är det som säger till om något gått sönder medan ingen
tittade.

Aktivera dem igen inför säsongsstart, i samma veva som säsongens schema läggs in och en dump
tas ([`DATABAS-BACKUP.md`](./DATABAS-BACKUP.md)).

---

## Genomförda larmtester

En rad per test. Testa om larmet någon gång byter kanal eller e-postadress.

| Datum | Utförd av | Resultat |
|---|---|---|
| *(fyll i vid första testet)* | | |
