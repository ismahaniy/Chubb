# ClaimDesk

A claims platform for motor and property claims across six APAC markets: Singapore, Hong Kong, Malaysia, Thailand,
Indonesia and the Philippines.

- **Claimants** report claims, track them and answer requests for information.
- **Officers** pick claims up, assess them, decide on them and settle them.
- **Managers** see liability exposure and officer workload.

One Angular app serves all three roles. The backend is .NET 10 and PostgreSQL 17, with Kafka for events.

## Prerequisites

| To… | You need |
|---|---|
| Run the backend stack | Docker with Compose v2 |
| Run the frontend | Node.js `^22.22.3`, `^24.15` or `>=26` (Angular 22's range), and npm |
| Develop and test the backend | .NET SDK 10.0.404 or later (pinned in `global.json`). Run `dotnet tool restore` once |

## Start

```bash
docker compose up -d --build                      # postgres, kafka, kafka-init, kafka-ui, api, worker
npm install --prefix frontend && npm start --prefix frontend    # http://localhost:4200
```

- On start-up the API applies the migrations and seeds the demo data (`Database__MigrateOnStartup=true` in compose).
- The worker starts once the API is healthy.
- `kafka-init` creates the topics and then exits.
- The frontend is **not** in compose. It runs on the Angular dev server, which proxies `/api` to `:5000`.
- Ports can be changed: copy `.env.example` to `.env`. Compose works without it.
- To start over with an empty database and empty topics: `docker compose down -v`.

## Seeded logins

Every demo user's password is **`Passw0rd!`**.

| Email | Role | Notes |
|---|---|---|
| `claimant1@claimdesk.test` | Claimant | Policies in Singapore, Malaysia and Indonesia. Has a claim waiting on information |
| `claimant2@claimdesk.test` | Claimant | Policies in Hong Kong, Thailand and the Philippines |
| `officer1@claimdesk.test` … `officer4@claimdesk.test` | Officer | Seeded claims are spread across all four |
| `manager@claimdesk.test` | Manager | |

- The seed is 6 markets, 10 policies and 15 claims covering every status.
- The source of truth is `backend/src/Claims.Infrastructure/Persistence/DbSeeder.cs`.
- `backend/requests.http` has a working call for every endpoint, using these users and policy numbers.

## URLs

| What | URL |
|---|---|
| Frontend | http://localhost:4200 |
| API | http://localhost:5000 |
| API docs (Scalar) | http://localhost:5000/scalar |
| OpenAPI document | http://localhost:5000/openapi/v1.json |
| Health (database and Kafka) | http://localhost:5000/health |
| Kafka UI | http://localhost:8080 |
| PostgreSQL | `localhost:5432`, user, password and database all `claims` |
| Kafka | `localhost:9092` from the host, `kafka:29092` from containers |

Scalar and the OpenAPI document are served only in the Development environment. Compose and `launchSettings.json`
both set Development.

## Developing

```bash
docker compose up -d postgres kafka kafka-init kafka-ui     # infrastructure only
dotnet watch --project backend/src/Claims.Api               # API on :5000, migrates and seeds itself
dotnet run --project backend/src/Claims.Worker              # outbox publisher and notification consumer
npm start --prefix frontend
```

- **Changing the schema:** `dotnet ef migrations add <Name> -p backend/src/Claims.Infrastructure -s backend/src/Claims.Api`
- **Changing the API:** build the backend. That regenerates `backend/openapi/claims-api.json`. Then run
  `npm run gen:api --prefix frontend` to refresh the TypeScript types. Commit both files.

## Tests

```bash
dotnet test backend           # Docker must be running: integration tests start PostgreSQL and Kafka with Testcontainers
npm test --prefix frontend    # Vitest
```

- **Backend unit tests** cover:
  - every allowed and forbidden claim transition
  - domain rules: coverage limit minus deductible, reserve before decision, currency checks, SLA
  - application services
  - exception mapping
  - the event envelope
  - the layer dependency rule
- **Backend integration tests** run against real PostgreSQL. They cover:
  - every endpoint
  - auth and claimant ownership (403/404)
  - two officers assigning the same claim at once (409)
  - outbox rows written with the change
  - exposure never summed across currencies
  - idempotent seeding
- **Kafka test:** a duplicate event produces only one notification.
- **Frontend:**
  - stores: auth, queue, claim, notifications
  - interceptors and the role guard
  - components: status badge, claim form, exposure table (checks that there is no cross-currency total)

## Scope

- **Claimant**
  - Report a claim against one of your own policies. Product and currency come from the policy.
  - List your claims, with "action needed" flags.
  - Claim detail with a timeline and the decision.
  - Answer information requests.
- **Officer**
  - Work queue with Unassigned and Mine tabs, market and status filters, and paging, ordered by SLA due date.
    Breaches are flagged and the queue refreshes every 10 s.
  - Assign a claim to yourself.
  - Request information.
  - Record an assessment (reserve and notes).
  - Approve up to the coverage limit minus the deductible, or reject.
  - Settle.
- **Manager**
  - Open liability exposure by market, product and currency. Currencies are never added together.
  - Per officer: open claims, SLA breaches, and claims settled in the last 7 days.
- **Everyone:** an in-app notification bell, fed by Kafka events through the worker.

Design is described in [docs/architecture.md](docs/architecture.md), which includes the event catalogue. The decision
records are in [docs/adr/](docs/adr/). How AI was used is in [docs/AI_JOURNAL.md](docs/AI_JOURNAL.md).

## Known gaps

- **Untested paths.** No automated tests for the outbox publisher (SKIP LOCKED, `sent_at`, back-off while Kafka is
  down), consumer retries or the dead-letter queue. No end-to-end test from API to Kafka to notification.
- **Event ordering.** If one row in a publish batch fails while a later row for the same claim succeeds, the failed
  event is sent later, out of order. Consumers deduplicate events but don't reorder them.
- **Stale screens.** No `If-Match`/ETag. `xmin` catches requests that race each other, but not an action taken from
  a screen that is out of date. The state-machine guards catch most of these.
- **Some bad input returns 409, not 400.** A lower-case currency code or an amount with three decimals passes DTO
  validation and is then rejected by `Money` as a domain error.
- **Polling, not push.** Notifications refresh every 15 s, the officer queue every 10 s and the dashboard every 30 s.
- **Manager notifications** have no claim page to link to.
- **Demo auth only.** Seeded users, a shared password and a symmetric JWT key in Development settings.
- **Single Kafka broker** with replication factor 1. Local use only. There is no deployment configuration.
- **The frontend is not in compose** (see Start).
