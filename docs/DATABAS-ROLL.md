# Databasroll och behörighetsgräns (least-privilege)

> **Läs det här när databasanslutningen sätts upp eller roteras.** Avsnittet
> [Verifiera](#verifiera-att-rollen-är-least-privilege) är en enda `SELECT` du kan klistra in i
> Neon-konsolen och läsa av på tio sekunder.
>
> Regelverk: [`CLAUDE.md`](../CLAUDE.md) §KM.11 · Säkerhetsbaslinjen (secrets, minsta behörighet).

---

## Varför det här finns

Det största intrånget i en jämförbar svensk idrottsapp berodde inte på en sofistikerad attack.
Rotorsaken var att applikationens **databaskonto hade för höga rättigheter** — det kunde köra
operativsystemskommandon via databasen och användes för att ta sig vidare i infrastrukturen
(lateral förflyttning). En enda oparametriserad fråga blev därför inte "en läckt tabell" utan
"en fot in i hela servern".

Vår motåtgärd är att anslutningen appen använder ska vara **så maktlös som funktionen tillåter**:
den ska kunna läsa och skriva sina egna tabeller, och ingenting mer. Då blir även en framtida bugg
begränsad till appens egen data — det finns ingen väg från databasrollen ut till maskinen.

Detta kompletterar de tekniska grindarna vi redan har: `RawSqlGuardTests` (ingen rå/interpolerad
SQL), `ChildProfileAllowlistTests` (ingen barn-PII), och att allt går via EF/parametriserade
frågor. Den här filen täcker lagret under dem — vad själva **kontot** får göra om något ändå
skulle gå fel.

---

## Vad appen faktiskt behöver

| Behov | Varför | Kräver superuser? |
|---|---|---|
| Ansluta över TLS | `sslmode=require` mot Neon | Nej |
| Läsa och skriva sina tabeller (DML) | All normal drift | Nej |
| Skapa/ändra sina tabeller (DDL) i `public` | Migrationerna körs vid uppstart (`Database:ApplyMigrationsOnStartup`) och äger `__EFMigrationsHistory` | Nej — ägarskap av det egna schemat räcker |
| Skapa extensions (`CREATE EXTENSION`) | **Behövs inte** — vi använder inga (`HasPostgresExtension`, `uuid-ossp`, `pgcrypto`, `postgis`, `citext` finns ingenstans i koden, verifierat) | — |
| Läsa serverfiler, köra OS-kommandon | **Ska aldrig behövas** — det var precis den vägen intrånget använde | Ja (nekas) |

Slutsatsen: appen behöver ett **vanligt, icke-superuser-konto som äger sitt eget schema**. Inget mer.

### Varför runtime-rollen ändå har DDL — ett medvetet val

Den strängaste uppdelningen ger *två* roller: en migrationsroll med DDL och en runtime-roll med
bara DML. Vi kör med **en** icke-superuser-roll som gör båda, eftersom migrationerna appliceras vid
uppstart och appen ändå äger hela sitt schema. Det är en medveten avvägning för ett ensamdrivet
projekt på Neons fria nivå — den gräns som betyder något (icke-superuser, ingen OS-/fil-/
extension-makt, ingen åtkomst till andra databaser) håller oavsett. Delar vi upp rollerna senare
är ändringen enkel: runtime-rollen tappar bara sin DDL-rätt.

---

## Plattformen gör redan en del av jobbet

Neon är hanterad Postgres, och det stänger av sig självt den värsta klassen:

- **Neon delar inte ut äkta superuser** till projektets roller. Standardrollen du får är redan
  `rolsuper = off`.
- **Postgres har ingen `xp_cmdshell`.** Motsvarigheterna — `COPY … FROM/TO PROGRAM`,
  `pg_read_server_files`, `pg_execute_server_program` — är superuser- eller
  predefined-role-skyddade och delas **inte** ut på Neons standardroller. OS-kommandovägen som
  fällde konkurrenten finns alltså inte tillgänglig för vår roll.
- **Anslutningen är TLS-tvingad** (`sslmode=require` i connection-strängen).

Vår uppgift blir därför inte att bygga skyddet från grunden, utan att **inte råka ge bort** det
plattformen redan gett oss: använd standardrollen, inte en uppgraderad admin, och peka den bara på
appens egen databas.

---

## Verifiera att rollen är least-privilege

Klistra in i Neons SQL-konsol (eller `psql` mot samma connection-sträng appen använder):

```sql
SELECT
  current_user                          AS roll,
  current_setting('is_superuser')       AS ar_superuser,   -- ska vara: off
  (SELECT rolcreaterole FROM pg_roles WHERE rolname = current_user) AS far_skapa_roller,
  current_database()                    AS databas,
  current_setting('server_version')     AS version;
```

Förväntat: **`ar_superuser = off`**. Är den `on` använder appen ett för mäktigt konto — byt
connection-strängen till Neons vanliga roll (den skapas åt dig per projekt) och rotera den gamla.

Kontrollera också att rollen inte fått ärva ett farligt predefined-role:

```sql
SELECT r.rolname
FROM pg_auth_members m
JOIN pg_roles r ON r.oid = m.roleid
JOIN pg_roles u ON u.oid = m.member
WHERE u.rolname = current_user
  AND r.rolname IN ('pg_read_server_files', 'pg_write_server_files', 'pg_execute_server_program');
```

Förväntat: **noll rader.** Får den träff har någon medvetet delat ut fil-/kommandoåtkomst —
återkalla den (`REVOKE <roll> FROM current_user;`).

---

## Behörighetsgränsen (bindande)

Anslutningssträngen appen använder (`ConnectionStrings__Default`, satt i Render — aldrig i koden,
§KM.11) ska peka på en roll som:

1. **inte är superuser** (`is_superuser = off`),
2. är begränsad till appens **egen** Neon-databas — ingen åtkomst till andra databaser eller
   projekt,
3. **aldrig** har `pg_read_server_files`, `pg_write_server_files` eller
   `pg_execute_server_program`,
4. **inte** behöver och **inte** har rätt att skapa extensions (vi använder inga),
5. äger sitt eget schema (`public`) så migrationerna kan köra DDL vid uppstart — men inte mer.

Skulle ett framtida behov kräva att rollen får någon av rättigheterna ovan är det ett
säkerhetsbeslut som ska skrivas in i [`PROJEKT-HANDOFF.md`](./PROJEKT-HANDOFF.md) under
*Viktiga beslut*, inte något som läggs till tyst i en connection-sträng.

---

## Om anslutningen läcker

Connection-strängen är en hemlighet som allt annat (§KM.11, secrets aldrig i repo). Läcker den:

1. **Rotera lösenordet** för rollen i Neon (Neon-konsolen → Roles → Reset password).
2. Uppdatera `ConnectionStrings__Default` i Render.
3. Eftersom rollen är least-privilege är skadeytan ändå begränsad till appens egen data — men
   rotera direkt, och läs [`DATABAS-BACKUP.md`](./DATABAS-BACKUP.md) om du behöver återställa.
