---
name: db-working-patterns
description: Safe patterns for ANY database work — reading, inspecting, querying, migrating (EF Core/other), seeding, dumping, restoring, or experimenting with Postgres/MySQL/SQLite/other DBs via docker containers, local servers, or SQL scripts. THE skill to load whenever a task touches a database in any way, even read-only or against a dev container. Enforces pre-flight state checks, backup-before-destructive, explicit user confirmation for wipes/reseeds, and transaction-safe experimentation.
---

# Database Working Patterns

Use this skill **every time** you touch a database with any tool (psql, docker exec, dotnet ef, dbeaver, scripts, hand-written SQL). This includes "harmless" tasks like reading row counts, and especially dev/test environments: **a dev database is still a database with data that may matter to the user.**

The core rule: **never destroy or mutate data without knowing what is there, protecting it, and getting explicit confirmation first.**

---

## 0. The Golden Rules (memorize these)

1. **Assume the data is valuable until proven otherwise.** A stopped container, a "dev" schema, or a test DB does NOT mean the data is disposable. Docker volumes persist across container restarts — check the volume, don't assume empty.
2. **Read the script before running it.** If a file does `DELETE`, `TRUNCATE`, `DROP`, `UPDATE` without a `WHERE`, or has a name like `seed.sql` / `reset.sql` / `wipe.sql`, treat it as destructive and say so out loud before executing.
3. **Back up before any destructive or bulk-mutating operation.** One `pg_dump` / `mysqldump` costs seconds; your user's data is irreplaceable.
4. **Get explicit confirmation for wipes/reseeds/migrations that reshape data.** "Explicit" = the user has said OK to *that specific script against that specific database*. A generic "update my seed file" or "check the DB" is NOT permission to run a wipe-and-reseed.
5. **Experiment inside a transaction.** Use `BEGIN; ... ROLLBACK;` for anything you are unsure about. Never `COMMIT` until verified.
6. **If something already went wrong: own it immediately.** State precisely what ran, against which database, what is affected, whether a backup exists, and what recovery options remain. Do not minimize, even in dev.

---

## 1. Pre-flight: know what you are about to touch

Before running ANY command, answer these:

| Question | How to check | Example |
|---|---|---|
| Which database engine & version? | `psql --version`, container image tag | postgres 16-alpine |
| Which database(s) exist? | `\l` / `SHOW DATABASES` | `medinilla`, `postgres` |
| Which container / host / port? | `docker ps`, compose file | `medinilla-postgres` @ 5432 |
| Does a named volume already hold data? | `docker volume ls`, `docker volume inspect <vol>` (check `CreatedAt`, size) | `dev_medinilla-pgdata` from weeks ago → **not empty** |
| Were migrations already applied? | `\dt`, `dotnet ef migrations list` / migration history table | table already exists → data may exist |
| Row counts in tables you'll touch? | `SELECT count(*) FROM ...` | 42 rows in `core_account` |
| Are there existing backups? | `ls` for *.dump/*.sql.gz, `\df`, pg_dumpall repos | none → say so |
| Is data likely real or disposable? | Ask the user if unsure. Never assume. | — |

Then **state your assessment to the user** before mutating anything: *"The DB at X has N rows in table T inside volume V created on DATE. I will [operation]. There is no backup. OK?"*

## 2. Read scripts fully before running them

If the task involves a SQL file or migration:

1. `read` the whole file (or at minimum grep for destructive keywords first):
   ```bash
   grep -nE "DELETE|TRUNCATE|DROP|UPDATE|INSERT|ALTER|CREATE OR REPLACE" /path/to/script.sql
   ```
2. Identify what it deletes, what it inserts, FK/dependency order, whether it resets sequences, and whether it's idempotent.
3. **Summarize the destructive steps to the user BEFORE execution.**

Example of what to look for (this exact trap hit a past session):
```sql
-- seed.sql: "wipes every public.core_* table" then inserts random data
DELETE FROM public.core_transactions_event;
DELETE FROM public.core_account;
```
That file is a **data-loss script**. Running it twice destroyed pre-existing rows in the `medinilla` dev DB.

## 3. Back up before destructive or bulk ops

Always offer/perform a backup first. For a dockerized Postgres:

```bash
# container name / engine / db
docker exec <container> pg_dump -U <user> -d <db> -Fc -f /tmp/backup.dump
docker cp <container>:/tmp/backup.dump ./backup-$(date +%Y%m%d-%H%M).dump
```

Bare-metal Postgres: `pg_dump -U <user> -d <db> -Fc -f backup.dump` (or `--format=plain` for readable SQL).

MySQL: `mysqldump -u <user> -p <db> > backup.sql`.

Also back up before:
- `dotnet ef database update` / other migration tooling that could drop/alter columns and fail halfway
- Truncate/reseed flows
- Any bulk `UPDATE`/`DELETE` you were asked to write

Put backups **outside** the container (docker cp / host path). Verify the dump quickly:
`docker exec <container> pg_restore -l /tmp/backup.dump | head` (or head the SQL dump).

## 4. Required confirmation gates

| Operation | Confirmation needed |
|---|---|
| Read-only inspect (SELECTs, `\d`, counts) | None — safe, but still identify the DB first |
| CREATE index / new table / additive migration | Tell user what & where; no backup needed if purely additive |
| INSERT of test/seed data | Fine on a DB the user designates as scratch; confirm target DB |
| `UPDATE`/`DELETE` with WHERE on existing rows | Backup + explicit OK |
| `DELETE` all rows / `TRUNCATE` / `DROP` / reseed / wipe scripts | Backup + explicit OK naming the script AND the DB |
| Restoring a backup (overwrites current state) | Explicit OK; confirm you are restoring into the intended DB |

If the user is not reachable to confirm, do the safe thing: **backup and stop** (leave the destructive script unexecuted).

## 5. Experimentation discipline

- Wrap unsure operations: `BEGIN; <sql>; ROLLBACK;` — verify with SELECTs before committing.
- Use `ON_ERROR_STOP=1` for scripted psql so failures don't silently continue.
- Run scripts read-only first where possible: `psql ... -c "\i script.sql"` inside a transaction if the file has no COMMITs, or process it through a dry-run/explain.
- Never pipe a destructive file into psql without having read it (step 2) and confirming (step 4).
- When a script crashes midway (e.g. `ERROR: "3" is not a valid binary digit`), stop, fix, and re-run — but re-running a wipe script means **data is already gone from the previous partial run**. Re-check state and re-confirm before retrying.

## 6. Container & volume awareness

- `docker compose up -d` recreates **containers**, not volumes. Named volumes persist — a freshly-started container can attach to months-old data.
- Check `docker volume inspect` `CreatedAt` and whether the DB already has tables/rows.
- `docker compose down -v` deletes volumes — never run it without explicit OK; it is a data-destroying command.
- If you need a guaranteed empty DB for seeding, that is a decision for the user (fresh volume vs existing).

## 7. When damage has already happened

1. Be straight, immediately, in the next message: what ran, against which DB, what it touched.
2. Scope precisely: databases, tables, and *what was NOT* affected (files, repo, other DBs).
3. Check for backups or a recovery path (existing dumps, `pg_dumpall`, WAL/point-in-time, cloud snapshots).
4. Offer restoration if a backup exists; otherwise help rebuild whatever the user needs and be clear that the old rows are gone.
5. Never bury the incident in a summary or minimize it.

## 8. Standard workflow (checklist)

Before each DB task:

```
[ ] Identify engine, container/host, port, database
[ ] Verify volume AND migration/row state (stopped container ≠ empty DB)
[ ] grep/read any SQL/migration files for destructive keywords
[ ] Check for existing backups; create one if destructive/bulk op
[ ] State plan + risk to user; get explicit OK for destructive ops
[ ] Use transactions/ROLLBACK for experiments; ON_ERROR_STOP for scripts
[ ] Re-verify state after (counts, FK integrity) and report
[ ] On any incident: own it first, scope it, offer recovery
```

## Reference: quick commands

```bash
# Inspect
docker exec <c> psql -U <u> -d <d> -c "\dt"
docker exec <c> psql -U <u> -d <d> -c "SELECT count(*) FROM <t>;"
docker volume inspect $(docker volume ls -q | grep pgdata)   # created? weeks old?

# Backup (docker)
docker exec <c> pg_dump -U <u> -d <d> -Fc -f /tmp/b.dump
docker cp <c>:/tmp/b.dump ./b-$(date +%Y%m%d-%H%M).dump

# Restore (docker) — overwrites! confirm first
docker cp ./b.dump <c>:/tmp/b.dump
docker exec <c> pg_restore -U <u> -d <d> --clean --if-exists /tmp/b.dump

# Safe experiment
docker exec -i <c> psql -U <u> -d <d> <<'SQL'
BEGIN;
-- ... risky sql ...
ROLLBACK;
SQL

# Migrations
dotnet ef migrations list                    # see what's applied
dotnet ef database update                    # only with backup + OK if schema reshaping
```