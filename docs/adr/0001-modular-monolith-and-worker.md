# 1. Modular monolith with a separate worker process

- Status: Accepted
- Date: 2026-11-27

## Context

ClaimDesk has one business domain: the claim lifecycle. Three roles use it, and a small number of asynchronous
side effects (notifications) hang off it. The build was time-boxed to five hours. Splitting this into
microservices would add network contracts, extra deployments and distributed failure modes before there is any
need for them. However, the asynchronous work (publishing the outbox, consuming Kafka) must not run on the request path.
It also has a different lifecycle from HTTP requests: long-running loops, back-off and rebalances.

## Decision

The system is one codebase and one database, run as **two processes**:

- `Claims.Api` serves HTTP for all roles.
- `Claims.Worker` runs the outbox publisher and the Kafka consumers.

Both are built from the same layered libraries:

| Project | Contains | References |
|---|---|---|
| `Claims.Domain` | Entities, the `Claim` state machine, domain events | nothing, and no packages |
| `Claims.Application` | One service method per use case, interfaces, DTOs | Domain |
| `Claims.Infrastructure` | EF Core, repositories, outbox, Kafka | Application, Domain |
| `Claims.Api` / `Claims.Worker` | Hosts | Infrastructure (for registration), Application |

The dependency direction is checked by `backend/tests/Claims.UnitTests/ArchitectureTests.cs`.
One Dockerfile builds both hosts, selected with a `PROJECT` build argument.

## Consequences

- **+** Business rules live in exactly one place, the `Claim` aggregate. The API and the worker cannot disagree about them.
- **+** One transaction boundary per use case, with no distributed transactions.
- **+** The worker can be scaled or restarted on its own. The outbox publisher uses `SKIP LOCKED`, so several
  publishers can run safely.
- **−** Both processes share one database schema, so they must be deployed together when the schema changes.
  Only the API migrates (ADR 0004).
- **−** The worker uses some Infrastructure types directly (the outbox dispatcher, the notification projection)
  instead of going through Application services. This is a deliberate shortcut, listed in the CLAUDE.md audit.

## Alternatives considered

- **Microservices per role or per capability:** rejected. One domain, one team, and a five-hour budget.
- **Background work hosted inside the API (`IHostedService`):** rejected. Kafka and outbox work would share the
  API's lifetime and scaling, and the rule "no Kafka on the request path" would be harder to see and to enforce.
