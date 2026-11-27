# AI Journal

How Claude Code was used to build ClaimDesk. Each prompt was one slice, run in plan mode first: Claude read
CLAUDE.md and the code, asked about any real ambiguity, and proposed a plan. I sent **every** first plan back with
the same challenge: *"Are you sure this plan is the most elegant and non-brittle solution? Check thoroughly."* The
second plan was the one approved. "Challenged" below records what that review changed.

### Prompt 1 — Repository scaffold
**Asked:** Scaffold the layout in CLAUDE.md: `Claims.slnx` with 5 src and 2 test projects referencing inward only;
`Directory.Build.props` (net10.0, nullable, warnings as errors); central package versions; and docker-compose with
PostgreSQL 17, Kafka 4.1 (KRaft, auto-create off), a topic-init container, Kafka UI and health checks.
**Accepted:** The stated layout, plus `global.json`, which pins SDK 10.0.404 so that an installed SDK 11 can't take
over. Compose works without a `.env` (every variable has a default). Topic creation is idempotent (`--if-not-exists`).
**Challenged:**
- The first attempt stopped because the machine had no .NET SDK and Node was below Angular 22's minimum. I
  installed both and restarted.
- The review replaced `dotnet new` templates with hand-written `.csproj` files. Templates emit `Version=` attributes,
  which break Central Package Management (NU1008).
- Package versions were pinned to 10.0.x rather than "latest", which had moved to 11.0.
- The Worker got Hosting from the shared framework instead of an unlisted NuGet package.
- Compose got `name: claimdesk` so the containers don't take their names from the repository folder.
**Overrode:** None. I approved adding the packages the stack needs that CLAUDE.md did not list:
`Microsoft.NET.Test.Sdk`, `xunit.runner.visualstudio`, the Testcontainers modules, `Mvc.Testing` and the Tailwind/PostCSS packages.

### Prompt 2 — Domain model and state machine
**Asked:** Claims.Domain only: entities, `Money`, enums, `DomainException`, and an event per transition. `Claim` owns its status
through `Assign`, `RequestInformation`, `ProvideInformation`, `RecordAssessment`, `Approve`, `Reject` and `Settle`.
An approval may not exceed the coverage limit minus the deductible, and a reserve is required before a decision.
Unit tests for every allowed and forbidden transition.
**Accepted:**
- Each method guards its own source status.
- Every guard runs before any change, so a rejected command leaves the status, the history and the events untouched.
  The tests assert all three.
- One private `MoveTo` is the only place `Status` is written.
- The maximum payable is copied from the policy at submission.
- Actor authorization stays in the services.
**Challenged:** The first draft used a generic `(from → to)` transition table. The review showed it would let
`Assign()` pass on an AwaitingInformation claim, because UnderReview is the target of both Assign and
ProvideInformation. It was replaced by explicit per-method guards. The review also had all times normalised to UTC
once, because Npgsql rejects non-UTC `timestamptz` values.


### Prompt 3 — Persistence, migration and seed data
**Asked:** `ClaimsDbContext` with snake_case names, one configuration per entity, enums as strings,
`numeric(18,2)` money, `xmin`, a unique claim number, the required indexes, and the outbox and processed-events
tables. The InitialSchema migration. Idempotent seeding: 6 markets, 2 claimants, 4 officers, 1 manager, about 10
policies and 15 claims. `MigrateAsync` behind `Database:MigrateOnStartup`.
**Accepted:**
- My answers to Claude's questions, all three the recommended option: add `Microsoft.EntityFrameworkCore.Design`
  and the `dotnet-ef` local tool, and add both to the CLAUDE.md allow-list; make markets a lookup table; generate
  claim numbers from a PostgreSQL sequence (`CLM-000001`).
- Claude flagged a deliberate deviation: `processed_events` keyed by `(event_id, consumer)`, so a second consumer
  of the same event is not skipped. I accepted it.
**Challenged:** The review fixed several brittle points:
- `MigrateOnStartup` moved from `appsettings.Development.json` to `launchSettings.json`. Otherwise every
  WebApplicationFactory test would try to migrate against localhost.
- A hand-written PBKDF2 hasher was replaced by ASP.NET's `PasswordHasher<User>`.
- Seed dates were made relative to the seeding time, so the demo doesn't stop working on 1 January.
- The money mapping committed to `ComplexProperty` instead of an "OwnsOne if this fails" fallback.
- Each test class got its own database container.
- Currency became `varchar(3)` rather than `char(3)`.
- Actor columns got foreign keys.
- The claim-number format was moved into one class.


### Prompt 4 — API cross-cutting concerns
**Asked:** ProblemDetails with an `IExceptionHandler` mapping; JWT bearer auth with role policies and
`POST /api/auth/login`; OpenAPI generated at build time into `backend/openapi/claims-api.json`; Scalar; `/health`
checking the database and Kafka; JSON logs with a correlation id; api and worker in compose; `requests.http`.
**Accepted:** As specified, plus `GET /api/auth/me` so auth could be proved before any claim endpoint existed. The
existing `.http` file was renamed to `requests.http`, and two small hand-written health checks were added because
no health-check package is on the allowed list.
**Challenged:** The review changed the following:
- The runtime image: from Alpine to the Ubuntu `aspnet` image plus curl, because librdkafka on musl is unreliable.
- The JWT options: bound lazily, so build-time OpenAPI generation needs no key, while Production with no key
  fails at start-up.
- Kafka health: one singleton admin client instead of a new client on every probe.
- `/health`, `/openapi` and `/scalar`: explicitly anonymous.
- Handled exceptions: logged by the handler, because .NET 10 stops logging them by default.
- The correlation id: the W3C trace id unless the inbound header passes a strict format check.
- Login: a dummy hash check for unknown emails, so response time doesn't reveal which users exist.
- Integration tests: one shared assembly fixture.


### Prompt 5 — Claimant slice (backend)
**Asked:** `POST /api/claims` (the policy must be active and cover the product), `GET /api/claims/mine`,
`GET /api/claims/{id}` with the timeline and open info requests (claimants see only their own claims), and the
info-request response endpoint. Events go to the outbox in the same SaveChanges. Specific repositories.
Integration tests with Testcontainers.
**Accepted:**
- My answers: identify the policy by `policyNumber`; "active" means in force today and covering the product.
  Both were the recommended options.
- Someone else's claim returns 403, and an unknown id returns 404.
**Challenged:** The review produced these fixes:
- "Today" is the market's local date (UTC+7 or UTC+8), not the UTC date, so a same-day incident isn't rejected at
  07:00 in Singapore.
- `ClaimsDbContext` refuses to save a claim whose events were never enqueued, so a forgotten outbox call fails
  loudly.
- Wire payloads became explicit records separate from the domain events, and a test checks that every event has
  one.
- Value-type DTO fields are nullable with `[Required]`, so validation actually runs.


### Prompt 6 — Officer slice (backend)
**Asked:** A paged work queue (unassigned or mine, market and status filters, sorted by SLA, breach flag); assign,
returning 409 if already taken, proved by a concurrent integration test; information requests; assessment (reserve
and notes); decision; settle. Each transition writes history and an outbox row in one transaction.
**Accepted:** "Breached" means open and past due (recommended). I accepted Claude's point that an approval also
needs a reason, as the domain already required. The `AddClaimSlaDueAt` migration includes a backfill written in
hours, so the result doesn't depend on the session time zone.
**Challenged:** The review cut new infrastructure. The existing single SaveChanges, `xmin` and the exception
handler already turn a lost race into a 409. The review also worked through both ways the two concurrent assigns
can interleave. Either the `xmin` UPDATE matches no row, or `Assign` sees the claim is already UnderReview. Both
give one 200 and one 409, so the HTTP test does not depend on timing. No new low-level test was needed, because
`ClaimPersistenceTests` already covers `xmin` deterministically.
**Overrode:** For the SLA, Claude recommended a fixed target per product (Motor 5 days, Property 10 days). I chose
**one 7-day SLA for every claim**, because it is simpler and enough for the scope.

### Prompt 7 — Manager reports
**Asked:** `GET /api/reports/exposure` (open claims, reserve or else claimed amount, grouped by market, product and
currency, never summed across currencies) and `GET /api/reports/workload` (per officer: open claims, SLA breaches,
settled in the last 7 days). Read-only, `AsNoTracking`, projections, and tests for the currency rule.
**Accepted:** My answer: "open" includes Approved but unsettled claims, which are still unpaid liability
(recommended). This gave one shared `Claim.ClosedStatuses` definition next to `OpenStatuses`.
**Challenged:** The review made currency part of the SQL `GROUP BY`, so the database cannot add two currencies
together. It also made the tests compare changes rather than seed totals: adding USD claims must leave the SGD line
unchanged and create a new line. The test repository is resolved through the real DI registration.


### Prompt 8 — Worker: outbox publisher and notification consumer
**Asked:** Follow the "Events and Kafka" section of CLAUDE.md exactly: the outbox publisher (SKIP LOCKED, oldest
first, `Acks=All`, idempotent producer, back-off, `sent_at`), envelope serialisation, and a notification consumer
(manual commit after the write, `processed_events`, DLQ after 3 failures). Add `GET /api/notifications`. One
Testcontainers test for duplicate events. Explain what happens on a crash between produce and `sent_at`.
**Accepted:** Delivery is at least once. A crash before the `sent_at` commit rolls back, so the row is produced again
and the consumer deduplicates it by `eventId`. This is written up in ADR 0003.
**Challenged:** The review changed the following:
- Produces start together instead of one by one, so the row locks are held for about one acknowledgement round trip.
- The dispatcher returns sent and failed counts instead of throwing.
- Deduplication is one SaveChanges plus a primary-key check, not raw `ON CONFLICT` SQL.
- One shared producer serves both the outbox and the DLQ.
- Each loop catches and backs off instead of crashing the host.
- A failed DLQ produce seeks back instead of skipping the message.
- Retries wait 1 s and then 2 s.
- The Worker reference in the test project uses an `extern alias`, so the API and Worker `Program` classes don't
  clash.


### Prompt 9 — Frontend foundation
**Asked:** A `gen:api` script (`openapi-typescript` → `api-types.ts`), `API_BASE_URL` from the environment, an
`AuthStore` built on signals, a login page listing the seeded users, auth and error interceptors (ProblemDetails →
toast), `roleGuard`, lazy role routes, and a neutral Tailwind shell named "ClaimDesk". Follow the smart/dumb rules.
**Accepted:** `openapi-typescript` as a dev dependency, approved because I named it. A scoped npm `overrides` entry
for its TypeScript 5 peer dependency, instead of `--legacy-peer-deps`. The demo-user list mirrors the seeder in one
file that names its source.
**Challenged:** The review changed the following:
- Stores don't navigate; pages do. The only exception is the 401 handler.
- A 401 logs the user out only when the request carried a token, so a failed login stays a toast.
- The HTTP status is read from the response, not from the generated `ProblemDetails.status`, which is typed
  `number | string | null`.
- The bearer token is attached only to requests to `API_BASE_URL`.


### Prompt 10 — Officer screens
**Asked:** A queue page (unassigned and mine tabs, filters, SLA badge, assign button, polling every 10 s) and a
claim workbench (detail, timeline, request-info form, assessment form, decision panel, settle). Dumb components for
the queue table, status badge, timeline, decision panel and money. Stores own the state. One store test and one dumb
component test.
**Accepted:** My answer: frontend only (recommended). The claim detail has no reserve or assigned officer, so
actions are gated by status, and server rules come back as 409/403 toasts.
**Challenged:** The review produced these fixes:
- `setInterval` with promises was replaced by `rxResource` keyed on a query signal, so a stale poll can't overwrite
  a newer filter.
- Stores are provided on the page, so polling ends with the page.
- After an error the workbench reloads, so the screen re-syncs with the server.
- A rejection sends a null amount, because the API returns 400 otherwise.
- One pure group validator replaced a `valueChanges` subscription.
- The dumb decision panel stopped injecting `NonNullableFormBuilder`, a service, which the dumb-component rule
  forbids.
- Money is formatted from the string value, so no precision is lost.
- Label maps are `Record<Enum, …>`, so the compiler catches a new enum value.


### Prompt 11 — Claimant screens, manager dashboard, notification bell
**Asked:** Claimant screens (submit a claim, my claims, claim detail with an info-request response), the manager
dashboard (exposure by market, product and currency; workload per officer), and a notification bell polling
`/api/notifications`. Reuse the shared dumb components.
**Accepted:** My answers, both recommended: add `GET /api/policies/mine`, so the form picks a policy and takes
product and currency from it instead of inviting 409s; add `POST /api/notifications/read` with an `upTo` timestamp,
so a notification that arrives after the list was shown stays unread. Timeline, decision and detail views moved to
`shared/ui`.
**Challenged:** The review found two gaps the screens alone could not close. No API listed a claimant's policies,
yet the domain requires product and currency to match the policy. And `Notification.MarkRead()` existed, but no
endpoint called it. The review raised them as questions, and my answers led to the two small endpoints above. The exposure table never shows a cross-currency total, and a component test checks this.


