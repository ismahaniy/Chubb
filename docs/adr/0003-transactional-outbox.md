# 3. Transactional outbox for domain events

- Status: Accepted
- Date: 2026-11-27

## Context

Every claim transition must change the database and also publish an event. Writing to PostgreSQL and then
producing to Kafka is a dual write, and it fails in two ways:

- the commit succeeds but the produce fails, so the event is lost;
- the produce succeeds but the commit fails, so an event is published for a change that never happened.

## Decision

**Write side, in the API:**
- The `Claim` aggregate collects domain events.
- The service calls `IOutbox.Enqueue(claim)`, implemented by `EfOutbox`. It turns the events into `outbox_messages`
  rows on the same `DbContext` and clears them from the aggregate.
- `SaveChanges` then commits the business rows, the status history and the outbox rows in **one transaction**.
- Safety net: `ClaimsDbContext.SaveChangesAsync` throws if any tracked claim still has events that were not
  enqueued. Forgetting the outbox fails in tests instead of silently dropping events.

**Publish side, in the worker (`OutboxPublisher` → `OutboxDispatcher`):**
- Every second it locks up to 100 unsent rows, oldest first, with `FOR UPDATE SKIP LOCKED`.
- It produces them with `Acks=All` and the idempotent producer.
- Rows Kafka acknowledged get `sent_at`. Rows that failed get `attempts` and `last_error`.
- It commits, then backs off exponentially, up to 30 s, while anything fails.

**Consume side:**
- Consumers record `processed_events(event_id, consumer)` in the same transaction as their own work.
- They commit the offset only after that.

## Consequences

- **+** No lost events and no phantom events. An event exists if and only if its change committed.
- **+** Kafka can be down for any length of time. Events wait in the table and publish in order of `occurred_at`.
- **−** Delivery is **at least once**. If the worker crashes after Kafka acknowledged a message but before the
  `sent_at` commit, the transaction rolls back, the lock is released and the row is produced again on the next poll.
  Consumers deduplicate by `eventId`.
- **−** Added latency is up to about one poll interval.
- **−** Ordering is only as strong as the batch. If an earlier row for a claim fails while a later row for the
  same claim succeeds, the earlier one is published later. This is listed in the README known gaps.
- **−** Sent rows pile up. There is no clean-up job yet.

## Alternatives considered

- **Produce from the service after `SaveChanges`:** the dual-write problem above.
- **Change data capture (Debezium) on the outbox table:** more robust ordering and less custom code, but another
  moving part to run, too heavy for this scope.
- **Kafka transactions:** they don't cover the PostgreSQL write.
