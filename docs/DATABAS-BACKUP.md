# Databasbackup och återställning

> **Läs det här när något har gått fel.** Avsnittet [Återställa](#återställa) är skrivet för
> att följas rakt av, uppifrån och ner, utan att du behöver förstå resten av dokumentet först.
>
> Regelverk: [`CLAUDE.md`](../CLAUDE.md) §KM.0 A2 · Säkerhetschecklistan rad 11.2

---

## Varför det här finns

Ett tappat matchschema mitt i säsongen kostar fyra tränare en kväll var att skriva in på nytt.
Därför är backup baslinje i det här projektet från dag ett, inte något som läggs till "vid behov".

**En otestad backup är ingen backup.** Rutinen nedan är genomförd på riktigt, inte bara skriven —
se [Genomförd återställningsövning](#genomförd-återställningsövning).

---

## Två lager

| Lager | Vad | Fönster | Täcker |
|---|---|---|---|
| **Neons historik (PITR)** | Automatisk, kontinuerlig | **6 timmar** | Ett misstag som upptäcks *samma förmiddag* |
| **Logisk dump** | `pg_dump` via [`scripts/Backup-Database.ps1`](../scripts/Backup-Database.ps1). Körs manuellt | Så länge du sparar filerna | Allt annat: Neon-projektet borta, kontot låst, byte av leverantör — och varje fel som upptäcks efter sex timmar |

### Sex timmar är kortare än det låter

Retentionen är **påslagen och satt till 6 timmar**, vilket är taket på Neons fria nivå
(bekräftat 2026-08-29). Alternativen är 0, 1, 2 eller 6 timmar; längre kräver betald plan.

Läs den siffran som det den är:

- En trasig migration som driftsätts **fredag kväll** och upptäcks **lördag morgon** ligger
  redan utanför fönstret. Det är dessutom exakt när appen används som mest.
- Säsongens schema som skrivs in i augusti och visar sig korrupt i november är långt utanför.
- Sover backenden gör den det tyst — ingen upptäcker ett datafel förrän någon öppnar appen.

**Därför är den manuella dumpen inte en reserv till reserven, utan det egentliga skyddsnätet.**
PITR täcker den olycka du märker direkt. Dumpen täcker resten.

### Varför dumpen inte tas automatiskt i CI

Det vore lätt att lägga en schemalagd GitHub Actions-körning som dumpar databasen och sparar
resultatet som en artefakt. **Det gör vi inte, med flit.**

Artefakter i ett publikt repo går att ladda ner av vem som helst som har länken. Databasen
innehåller i dag inga personuppgifter alls, men så snart en tränare lägger upp truppen ligger
barns förnamn där (§KM.1). En automatiserad läcka av barnuppgifter vore precis det som
§KM.2 och §KM.1 finns för att förhindra.

Neons PITR är det automatiska lagret. Den logiska dumpen tas manuellt, av en människa, till en
katalog utanför repot.

---

## Rutin

| När | Vad |
|---|---|
| **Före varje migration mot produktion** | `./scripts/Backup-Database.ps1`. **Inte förhandlingsbart** — PITR-fönstret på 6 timmar hinner löpa ut innan ett migrationsfel nödvändigtvis upptäcks |
| **Kvartalsvis, och före säsongsstart** | `./scripts/Backup-Database.ps1 -VerifyRestore` — hela övningen. Skriv in datum och resultat längst ned i det här dokumentet |
| **Efter att säsongens schema lagts in** | En dump. Det är årets mest kostsamma data att skriva in igen |

Dumparna hamnar som standard i `~/KarraMatcher-backups`, alltså **utanför repot**. De ska aldrig
checkas in — `.gitignore` blockerar `*.dump` som skyddsnät, men den riktiga regeln är att de inte
hör hemma i ett publikt repo.

### Ta en dump

```powershell
./scripts/Backup-Database.ps1
```

Anslutningssträngen läses ur `dotnet user-secrets`. Skriv aldrig in den på kommandoraden — då
hamnar den i PowerShells historik.

Skriptet kräver bara Docker. PostgreSQLs klientverktyg körs i containrar, så inget behöver
installeras lokalt.

### Bevisa att dumpen går att återställa

```powershell
./scripts/Backup-Database.ps1 -VerifyRestore
```

Dumpen återställs i en färsk PostgreSQL-container och jämförs mot källan på fyra punkter:
radantal per tabell, kolumnschema, nycklar och constraints, samt index. Skiljer sig något
avbryts körningen med vilket av dem det var.

Skriptet rör aldrig källdatabasen. Det läser.

---

## Återställa

### Läge 1 — data raderad eller trasig migration, Neon fungerar

Det här är det vanliga fallet och det snabbaste.

1. Logga in på [Neon-konsolen](https://console.neon.tech) och välj projektet.
2. Gå till **Branches** → **New branch**.
3. Välj **Create from a point in time** och ange tidpunkten **strax före** felet.
   Ligger tidpunkten mer än **sex timmar** bakåt finns den inte kvar — hoppa till
   [Läge 2](#läge-2--neon-projektet-är-borta) och använd senaste dumpen i stället.
4. Ge branchen ett namn som säger vad det är, t.ex. `restore-2026-08-29-fel-migration`.
5. Skapa branchen. Den får en egen anslutningssträng.
6. **Verifiera innan du byter.** Anslut mot den nya branchen och kontrollera att datan ser rätt ut:

   ```powershell
   docker run --rm -e PGPASSWORD=<losenord> postgres:18-alpine `
     psql "postgresql://<user>@<ny-host>/KarraMatcher?sslmode=require" `
     -c 'select count(*) from "Matches";'
   ```

7. Stämmer det: uppdatera `ConnectionStrings__Default` i **Renders dashboard** till den nya
   branchens sträng och gör en **Manual Deploy**.
8. Behåll den gamla branchen några dagar. Radera inget förrän du är säker.

### Läge 2 — Neon-projektet är borta

1. Skapa ett nytt PostgreSQL 18-projekt, hos Neon eller någon annan. Region inom EU (§KM.6).
2. Skapa en tom databas som heter `KarraMatcher`.
3. Återställ den senaste dumpen:

   ```powershell
   docker run --rm -e PGPASSWORD=<losenord> -v "$HOME\KarraMatcher-backups:/backup" `
     postgres:18-alpine `
     pg_restore --no-owner --no-acl `
     -d "postgresql://<user>@<ny-host>/KarraMatcher?sslmode=require" `
     /backup/<dumpfil>.dump
   ```

4. Kontrollera radantalen mot [tabellen nedan](#genomförd-återställningsövning).
5. Uppdatera `ConnectionStrings__Default` i Render och gör en **Manual Deploy**.
6. Kontrollera att `/health/ready` svarar `200 Healthy` — den går hela vägen ner i databasen.

> **`--no-owner --no-acl` är inte valfritt.** Dumpen skapades med samma flaggor. Neons roller
> finns inte i en ny databas, och utan dem faller återställningen på rättighetsfel som inte har
> med datan att göra.

### Om appen inte startar efter en återställning

Kontrollera i den här ordningen:

1. Svarar `/health` men inte `/health/ready`? Då lever processen men når inte databasen —
   felet ligger i anslutningssträngen, inte i datan.
2. Saknas `SSL Mode=Require;Channel Binding=Require` i strängen? Neon vägrar okrypterat.
3. Är strängen i **nyckelord-format** (`Host=...;Database=...`)? Npgsql förstår inte
   `postgresql://`-URI:er. Neon visar URI som standard — välj **.NET** i formatväljaren.
4. Står det `Anslutningssträngen 'Default' saknas` i Renders logg? Då är miljövariabeln inte
   satt alls. Den är `sync: false` i `render.yaml` och fylls i manuellt.

---

## Genomförd återställningsövning

**2026-08-29** — full övning genomförd, inte simulerad.

Dump från produktionsdatabasen på Neon (PostgreSQL 18.6), återställd i en tom PostgreSQL
18-container. Jämförelse mellan källa och återställd kopia:

| Kontroll | Resultat |
|---|---|
| Radantal per tabell | Identiskt |
| Kolumnschema (namn, typ, nullbarhet) | Identisk md5 |
| Nycklar och constraints | 10 mot 10 |
| Index | 13 mot 13 |
| Innehåll i `Matches` (md5 över alla rader) | `3c6310b4…` mot `3c6310b4…` — identiskt |

Radantal vid övningen:

| Tabell | Rader |
|---|---|
| `Clubs` | 1 |
| `AgeGroups` | 1 |
| `Teams` | 4 |
| `Venues` | 7 |
| `Matches` | 25 |
| `__EFMigrationsHistory` | 1 |

**Utöver jämförelsen kördes den skarpa API-imagen mot den återställda databasen.**
`/health` och `/health/ready` svarade båda `200 Healthy`, det senare efter en riktig fråga mot
databasen. Det är skillnaden mellan "datan ser rätt ut" och "applikationen fungerar på den".

Dumpstorlek: 12 667 byte. Ett skalprov — den växer med säsongens matcher, inte med antalet
användare.

### En fälla som övningen avslöjade

Första körningen föll på `pg_restore: the database system is shutting down`. PostgreSQLs
officiella Docker-image startar en **tillfällig** server under initieringen som bara lyssnar på
unix-socketen, och stänger sedan ner den för omstart. En `pg_isready` mot socketen svarar då ja
på en server som är på väg att försvinna.

Skriptet väntar därför på **TCP-porten**, som öppnas först när databasen faktiskt är klar. Värt
att veta om du någon gång skriver egna skript mot samma image.

### Kommande övningar

Fyll på tabellen. En rad per genomförd övning.

| Datum | Utförd av | Resultat |
|---|---|---|
| 2026-08-29 | Haval | ✅ Alla fyra kontroller identiska; API kördes mot återställd databas |
