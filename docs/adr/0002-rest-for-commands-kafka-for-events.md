# 2. REST for commands and queries, Kafka for events

- Status: Accepted
- Date: 2026-11-27

## Context

Users need an immediate, definite answer to what they do. Assigning a claim either works or fails with a 409
because someone else took it. Approving a claim either works or fails because the amount exceeds what the
policy pays. Other work follows from a change that has already happened and can run later: telling the
claimant, and in future audit trails, analytics or payments.

## Decision

- **REST (ASP.NET Core controllers)** for every user command and every read.
  - Errors are RFC 7807 ProblemDetails: 400 validation, 403 forbidden, 404 not found, 409 domain rule or concurrency.
  - The contract is published as OpenAPI (ADR 0005).
- **Kafka** only for **domain events**: facts that have already been committed, such as `ClaimSubmitted` and `ClaimApproved`.
  - Topic `claims.events` has 6 partitions and is keyed by claim id, so the events of one claim stay in order on one partition.
  - Failed messages go to `claims.events.dlq`.
- **The API never produces to Kafka.** It writes events to the outbox in the same transaction as the change
  (ADR 0003). Only the worker talks to Kafka.
- Kafka is not used for request/response, and the API does not read from Kafka.

## Consequences

- **+** HTTP latency and availability do not depend on Kafka. Claims can still be submitted while the broker is down.
  `/health` reports the outage and the outbox catches up once Kafka is back.
- **+** New consumers can be added without changing the API.
- **−** Event effects are eventually consistent. A notification appears a few seconds after the action, and the
  SPA picks it up by polling.
- **−** Delivery is at least once, so every consumer must be idempotent (`processed_events`).
- **−** Two integration styles to understand and test.

## Alternatives considered

- **Everything over Kafka (commands as messages):** rejected. Users need synchronous answers to state-machine
  and concurrency errors.
- **Everything synchronous (send notifications inside the request):** rejected. It couples requests to side effects
  and fails the request when a side effect fails.
- **Push to the browser (SignalR/SSE):** deferred. Polling was enough for the scope (see README known gaps).
