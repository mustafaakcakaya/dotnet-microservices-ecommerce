# ADR 0002 - The CDC worker runs as a single instance for now

- **Status:** Accepted (temporary constraint - horizontal scaling to be addressed later)
- **Date:** 2026-09-03
- **Scope:** Ordering.Worker

## Context

The worker keeps a single "last processed LSN" per `ConsumerName` in
`dbo.OutboxCdcCheckpoints`. If two instances run concurrently under the same `ConsumerName`:

- both read from the same checkpoint and **publish the same messages** (needless duplicates),
- the last writer wins when saving the checkpoint, so a slower instance can **move it backwards**
  and cause even more redelivery.

Consumer idempotency (ADR 0001) keeps duplicates from corrupting data, but they still cost work
and make logs and metrics harder to read.

## Decision

Run **exactly one CDC listener instance**. Compose and deployment definitions declare the worker
as a single replica; scaling is vertical - `BatchSize` and `PollingInterval` - rather than
horizontal.

## Future work

More than one instance will be wanted. The minimum design for that:

1. **Leader election / lease:** add `LeaseOwner` and `LeaseExpiresOnUtc` to the checkpoint table
   and have each instance renew the lease periodically; an instance without the lease waits.
   (SQL Server's `sp_getapplock` would also work; a lease table is better for observability.)
2. Write the checkpoint with `WHERE LeaseOwner = @Me AND LeaseExpiresOnUtc > SYSUTCDATETIME()`
   so it can never regress.
3. Alternatively, partition per `ConsumerName` (for example by a hash range of `AggregateId`) for
   real parallelism. CDC reads are LSN-ordered, so this is more involved and needs care to
   preserve per-aggregate ordering.

This work is **out of scope** for the current change and will get its own ADR and implementation.

## Note

A polling-based reader (`SELECT ... FOR UPDATE SKIP LOCKED`) would remove this constraint by
itself, but it was not chosen because it conflicts with the goal of learning and porting CDC. It
could be added later as a second reading strategy over the same outbox table, for comparison.

## Related

- ADR 0001 - At-least-once delivery and consumer-side idempotency
