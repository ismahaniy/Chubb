# 4. EF Core owns the database schema

- Status: Accepted
- Date: 2026-11-27

## Context

The schema needs to change with the code, slice by slice, and the model needs a few PostgreSQL details:

- snake_case names
- `xmin` as the row version
- `numeric(18,2)` money
- a claim-number sequence
- partial indexes

Two sources of truth, such as hand-written SQL plus an ORM model, drift apart. This project has no DBA review step.

## Decision

- **The EF Core model in `Claims.Infrastructure` is the only schema definition.**
  - It has one `IEntityTypeConfiguration` per entity.
  - Naming is snake_case (`EFCore.NamingConventions`). Enums are stored as strings.
  - Money is a complex property, `numeric(18,2)` plus a 3-letter currency.
  - `xmin` is a shadow row-version property on `claims`.
- **Migrations are generated**:
  - Command: `dotnet ef migrations add <Name> -p backend/src/Claims.Infrastructure -s backend/src/Claims.Api`.
  - Generated migrations are committed and reviewed like code.
  - Hand edits are limited to data backfills, for example `sla_due_at`.
- **Only the API migrates, and only behind a flag:**
  - It calls `MigrateAsync()` at start-up only when `Database:MigrateOnStartup=true`.
  - The flag is false in `appsettings.json` and set to true by compose and `launchSettings.json`.
  - The worker never migrates. In compose it waits for the API to be healthy.
- **Seeding is idempotent:**
  - Seeding is wired through `UseAsyncSeeding` and runs under EF's migration lock.
  - It uses the aggregate's own methods, so seeded claims obey the state machine.
  - It saves in one transaction and skips the run if any user exists.
  - Seed events are cleared, so seeding never publishes to Kafka.

## Consequences

- **+** Model drift is caught. EF 10 `MigrateAsync` fails when the model has changes with no migration, and the
  integration tests run real migrations on PostgreSQL 17 through Testcontainers.
- **+** A fresh clone becomes a working, seeded database with one `docker compose up`.
- **−** Migrating at start-up suits a demo, not production with several replicas. There, migrations should be a
  separate deployment step. The flag exists so that step can be added.
- **−** Generated SQL is only reviewed if someone reads the migration diff.
- **−** Only async seeding is configured. A synchronous `Migrate()` would not seed.

## Alternatives considered

- **SQL-first migrations (DbUp/Flyway) and a mapped model:** two sources to keep in sync, and an extra tool that
  isn't on the allowed package list.
- **`EnsureCreated`:** no upgrade path for an existing database.
