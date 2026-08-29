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
| Fönstrad ping (vår lösning) | **~251** | **~499 timmar** |

Sex timmars marginal betyder att en enda extra fri tjänst, en serie omdeployer eller en
trafiktopp kan släcka hela appen i upp till trettio dagar. Det är ett dåligt byte för en
app som hundra familjer förlitar sig på.

---

## Lösningen: pinga när folk faktiskt är där

Tjänsten hålls vaken när appen används, och tillåts sova när den inte gör det.

| När | Intervall | Varför |
|---|---|---|
| **Fredag 15:00 – söndag 22:59** | var 5:e minut | Matchhelgen. Ingen förälder ska möta en kallstart lördag morgon |
| **Varje dag 14:50** | tre anrop på sex minuter | Daglig hjärtslagskontroll — och det som gör att fredagsfönstret startar varmt |

**Räkningen:** 56 timmar i helgfönstret plus ~1,4 timmar för vardagarnas kontroll =
~58 timmar i veckan, alltså **~251 timmar i månaden**. Marginalen till taket är ~499 timmar.

### Vad det kostar

En tränare som redigerar schemat en tisdagskväll kan få vänta ungefär en minut på att
tjänsten vaknar. Det är **en person som gör en planerad sak**, inte hundra föräldrar som
snabbt vill se en matchtid. UI:t ska säga ifrån på svenska vid långsamt svar (§KM.11), inte
visa en spinner som ser trasig ut.

Utanför helgen upptäcks ett avbrott vid nästa dagliga kontroll, alltså inom ett dygn. Under
helgen, när det spelar roll, är det fem minuter. Det är en medveten avvägning: en app som
ingen öppnar på en tisdagsnatt har inte samma brådska som en som hundra föräldrar väntar på
en lördagsmorgon.

### Edge-cachen bär det vanliga fallet

Publika GET-svar cachas på Vercels edge (`#13`), så lagets schema och ICS-feeden kan besvaras
**utan att Render väcks alls**. Övervakningen är därför ett komplement, inte det enda som står
mellan en förälder och en kallstart.

---

## Verktyget

**[cron-job.org](https://cron-job.org)** — fri nivå, upp till en körning per minut, egen
schemaläggning per veckodag och timme, och avisering både när ett jobb börjar fallera och när
det fungerar igen. EU-baserad tjänst.

### Tre gränser som formar hela upplägget

| Gräns | Konsekvens för oss |
|---|---|
| **30 sekunders timeout, går inte att höja** | Render tar upp till en minut att vakna. Ett anrop mot en sovande tjänst kan alltså timea ut även när allt fungerar |
| **Jobb stängs av automatiskt efter fler än 25 misslyckanden i rad** | Ett jobb som alltid möter en kallstart slutar tyst att existera |
| 64 kB maximalt svar | Ofarligt här — `/health` svarar med ordet `Healthy` |

Den första gränsen är den som avgör utformningen nedan, och den är lätt att gå bort sig på:
**ett jobb som pingar en sovande tjänst kommer att larma fastän ingenting är fel.**

### Varför inte GitHub Actions

En schemalagd workflow hade varit gratis och legat i repot. Två saker gör den olämplig:
GitHub **stänger av schemalagda workflows efter 60 dagars inaktivitet i repot**, vilket är en
realistisk risk för en säsongsapp som ligger stilla över vintern. Och GitHubs cron är
best-effort — körningar försenas eller hoppas över vid belastning, vilket är precis fel
egenskap för något som ska hålla en tjänst vaken.

---

## Sätta upp

Alla jobb pekar på `/health` — **aldrig** på en tung endpoint, och aldrig på `/health/ready`,
som går ner i databasen och skulle belasta Neon i onödan:

```
https://karramatcher-api.onrender.com/health
```

Adressen är backendens Render-URL direkt, inte Vercel-domänen. `/health` ligger utanför
`/api`-rewriten och proxas inte.

### Mönstret: väck först, larma sedan

Ett enda jobb som både väcker tjänsten och larmar när den inte svarar går inte att få tyst.
Väckningen tar upp till en minut, timeouten är 30 sekunder, och resultatet blir ett larm i
timmen som ingen orkar läsa.

Därför delas jobbet i två:

- **Uppvärmningsjobbet** anropar tjänsten och **har notiser avstängda**. Att det ibland timear
  ut är väntat och ointressant — anropet väcker Render ändå.
- **Hälsokollen** kommer några minuter senare, när tjänsten redan är vaken, och **har notiser
  påslagna**. Svarar den inte då är något verkligen fel.

Uppvärmningen gör **två** anrop, inte ett. Det andra lyckas nästan alltid, vilket nollställer
räknaren för misslyckanden i rad — annars stängs jobbet av automatiskt efter 25 dygn.

### De fem jobben

| # | Title | Veckodag | Timmar | Minuter | Notiser |
|---|---|---|---|---|---|
| 1 | `karra-uppvarmning` | Alla | 14 | 50 och 53 | **AV** |
| 2 | `karra-halsokoll` | Alla | 14 | 56 | **PÅ** |
| 3 | `karra-helg-fredag` | Endast fredag | 15–23 | var 5:e | **PÅ** |
| 4 | `karra-helg-lordag` | Endast lördag | 0–23 | var 5:e | **PÅ** |
| 5 | `karra-helg-sondag` | Endast söndag | 0–22 | var 5:e | **PÅ** |

Jobb 1 och 2 är den dagliga hjärtslagskontrollen. De fyller dessutom en andra funktion:
klockan 14:50 på fredagen värms tjänsten upp, så att helgfönstret som startar 15:00 möter en
**vaken** tjänst i stället för en kallstart. Utan det hade veckans första helgping larmat.

Jobb 3–5 håller tjänsten vaken hela matchhelgen. Efter det första anropet är den varm, så
alla efterföljande svarar på bråkdelen av en sekund — ett larm därifrån är alltid äkta.

Helgen hänger ihop av sig själv: fredagens fönster löper in i lördagen, och lördagens in i
söndagen. Tjänsten hinner aldrig somna däremellan.

### Steg för steg, per jobb

1. **Create cronjob**.
2. **Title** och **URL** enligt tabellen ovan.
3. Under **Execution schedule**, välj **Custom** och kryssa i veckodagar, timmar och minuter.
4. Under **Notifications**: slå på både *failure* och *success after failure* — utom på jobb 1,
   där **allt ska vara avstängt**.
5. Låt **Request timeout** stå på maxvärdet. Det är 30 sekunder och går inte att höja.
6. **Create**.

> Slår du på notiser för jobb 1 får du ett falsklarm varje gång tjänsten vaknar. Det är hela
> anledningen till att jobbet finns.

---

## Verifiera att larmet fungerar

Ett larm som aldrig prövats är en förhoppning, inte ett larm.

1. Gå till Renders dashboard → tjänsten → **Suspend Service**.
2. Vänta till nästa körning av **`karra-halsokoll`** (kl 14:56). Vill du inte vänta ett dygn:
   gör testet under helgen, då går något av helgjobben inom fem minuter.
3. **Kontrollera att mejlet faktiskt kom fram** — inklusive skräpposten. Kom det inte fram är
   larmkanalen inte verifierad, oavsett vad cron-job.org visar i sitt gränssnitt.
4. **Resume Service** i Render.
5. Kontrollera att återhämtningsmejlet kommer.
6. Skriv in datum och resultat i tabellen längst ned.

---

## Utanför säsong

Fotbollssäsongen sträcker sig ungefär april till oktober. Under vintern finns inga matcher,
och helgfönstret fyller ingen funktion.

**Pausa jobb 3–5** i cron-job.org när säsongen är slut. Låt `karra-uppvarmning` och
`karra-halsokoll` gå — tillsammans kostar de under 10 timmar i månaden och är det som säger
till om något gått sönder medan ingen tittade.

Aktivera dem igen inför säsongsstart, i samma veva som säsongens schema läggs in och en dump
tas ([`DATABAS-BACKUP.md`](./DATABAS-BACKUP.md)).

---

## Genomförda larmtester

**2026-08-30, 00:24 — larmet verifierat på riktigt.**

Tjänsten suspenderades i Renders dashboard medan `/health` mättes utifrån var 15:e sekund:

```
00:24:04  200        senast uppe
00:24:19  503   ┐
   ...           |   nedtid: 1 min 45 s
00:25:49  503   ┘
00:26:04  200        tillbaka
```

**Larmmejlet kom 00:25**, alltså inom en dryg minut från att tjänsten gick ner. Det var
`karra-helg-sondag` som fångade felet — den går var femte minut, och körningen kring 00:25
låg inuti nedtidsfönstret.

### Lärdom om själva testet

Nedtiden blev bara 105 sekunder, och helgjobben går var femte minut. Fönstret rymde alltså
**en enda** möjlig körning. Den träffade — men hade den inte gjort det hade utfallet varit
tvetydigt: uteblivet mejl kan lika gärna betyda "larmet är trasigt" som "ingen körning hann
med".

**Låt tjänsten ligga nere i minst sex minuter nästa gång**, så att minst en körning garanterat
träffar. Under vardagar, när bara `karra-halsokoll` går klockan 14:56, gäller i stället att
testet måste omfatta den tidpunkten.

### Tabell

En rad per test. Testa om larmet någon gång byter kanal eller e-postadress.

| Datum | Utförd av | Resultat |
|---|---|---|
| 2026-08-30 | Haval | ✅ Larm inom 1 min, och återhämtningsmejl efter omstart. Nedtid verifierad utifrån: 00:24:19–00:25:49 |
