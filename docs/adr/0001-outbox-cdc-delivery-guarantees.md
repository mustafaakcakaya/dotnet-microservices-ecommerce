# ADR 0001 - Delivery guarantees: at-least-once with consumer-side idempotency

- **Status:** Accepted
- **Date:** 2026-09-03
- **Scope:** Ordering outbox flow, Ordering.Worker (CDC listener), all consumers

## Context

The Ordering service writes an aggregate change and the integration event to be published in the
same SQL transaction (Transactional Outbox). A separate worker reads the INSERTs that reach
`dbo.OutboxMessages` through SQL Server CDC, publishes them to the message broker, and advances
its CDC checkpoint (the last processed LSN) once publishing succeeded.

Publishing and writing the checkpoint are two operations and cannot be made atomic; the broker
itself cannot join the transaction either. That forces a choice between three options.

## Decision

**Adopt at-least-once delivery and remove duplicates on the consumer side through the inbox
pattern.**

Concretely:

1. The checkpoint advances **only** after every message in an LSN group (one source transaction)
   has been published. If publishing fails the checkpoint stays put and the message is read again.
2. If the worker crashes after publishing but before writing the checkpoint, the message is
   published a second time. This is **accepted**: redelivery is preferred over loss.
3. Every integration event's `Id` travels to the broker as the message id, and the identity of a
   message never changes along the way - outbox row, CDC record, broker message.
4. Consumers record that id in an **inbox** table and refuse to process the same message twice.
   `BuildingBlocks.Messaging` provides the store and a MassTransit filter for this; anyone
   writing a new consumer is required to use them.

## Alternatives considered

| Alternative | Why not |
| --- | --- |
| **At-most-once** (checkpoint first, then publish) | A crash before publishing loses the message permanently. Losing an order event is not acceptable to the business. |
| **Exactly-once** (2PC / XA, enlisting the broker) | A distributed transaction between SQL Server and RabbitMQ/Kafka adds operational complexity and cost; on the Kafka side it means transactional producers plus consumer offset management. Not worth it at this scale. |
| **Leave deduplication to the broker** | RabbitMQ has no built-in deduplication; Kafka's idempotent producer only covers producer retries, not a worker restart. |

## Consequences

**Positive**
- No message loss; the flow recovers on its own after worker, broker or database outages.
- The publishing side stays simple - no distributed transactions - and is easy to test.

**Costs and caveats**
- Every consumer **must** be idempotent; one that is not will do duplicate work.
- The inbox table needs a retention or cleanup policy as it grows.
- Events are ordered per aggregate (Kafka keys on `AggregateId`); there is no global ordering.
- Only a single CDC listener instance runs today (see ADR 0002).

## Verification

This was confirmed live: with the broker stopped the checkpoint did not move, and once the broker
came back the message was published automatically and only then did the checkpoint advance. The
same scenario is covered by an integration test against a real SQL Server with CDC enabled.

## Related

- ADR 0002 - Single CDC worker instance and the plan for horizontal scaling
