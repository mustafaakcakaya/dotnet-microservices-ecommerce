# ADR 0003 - Checkout is published from Basket through a transactional outbox

- **Status:** Accepted
- **Date:** 2026-09-20
- **Scope:** Basket.API checkout, Ordering's BasketCheckoutEvent consumer

## Context

Checkout used to publish the event and then delete the basket:

```csharp
await publishEndpoint.Publish(eventMessage, cancellationToken);
await repository.DeleteBasket(userName, cancellationToken);
```

Two separate writes, no transaction between them. That gives three problems:

- **A crash in between sends the event and keeps the basket.** The customer sees a
  basket that was already ordered, checks out again, and gets a second order for the
  same cart. The event ids differ, so nothing downstream can tell the two apart.
- **The request depends on the broker.** With RabbitMQ down, checkout failed even
  though the database was healthy and had everything needed to accept the order.
- **Two concurrent requests both publish.** Both read the basket, both publish, both
  delete - two orders from one double click.

ADR 0001 established at-least-once delivery with consumer-side idempotency for the
Ordering outbox, but its scope stopped at Ordering. Checkout was a second publishing
path that quietly did not follow it.

Ordering solves this with SQL Server CDC (ADR 0001). Basket cannot copy that: it
persists through Marten on PostgreSQL, where the equivalent is logical decoding.

## Decision

**The basket deletion and the checkout event are written in one Marten transaction,
and a relay publishes from that outbox afterwards. The request publishes nothing.**

1. `BasketRepository.CheckoutBasket` loads the basket **from the database** - never
   from the Redis cache, so a stale entry cannot check the same basket out twice -
   deletes it, stores a `BasketOutboxMessage`, and calls `SaveChangesAsync` once.
   The basket disappears if and only if the event is recorded.
2. `BasketOutboxRelay`, a hosted service, publishes due messages oldest first,
   backs off exponentially on failure, and marks a message poisoned once it has
   failed `MaxPublishAttempts` times so one bad event cannot block the rest.
3. Delivery is **at-least-once**, consistent with ADR 0001. A message is removed
   only after it was published, so a crash in between republishes it under the same
   MessageId and Ordering's inbox drops the duplicate.
4. The event id is derived from the user and the exact basket version being checked
   out (`CheckoutId.For`). Two requests racing on one basket derive the same id and
   upsert a single message; a basket filled again afterwards is a new version and
   gets a new id.
5. Published messages are **deleted**, not marked as sent. The event carries payment
   details and there is nothing to gain from keeping them at rest.
6. Basket refuses to start unless `MessageBroker:Provider` is RabbitMq, because
   Ordering consumes the checkout with a MassTransit consumer; publishing into Kafka
   would succeed while nothing listened.

## Alternatives considered

| Alternative | Why not |
| --- | --- |
| **Leave it as it was** | The dual write is the defect; retry logic around the publish does not fix a crash between the two writes. |
| **PostgreSQL logical decoding**, mirroring Ordering's CDC | Closest to Ordering, but its failure mode is worse here: a stalled consumer keeps a replication slot, the slot retains WAL, and the database fills up. Polling a small table costs a query every couple of seconds and fails harmlessly. |
| **MassTransit's built-in EF Core outbox** | Basket persists through Marten. The outbox would have to share a transaction with Marten's session to be atomic, which means adding EF Core purely for the outbox and coordinating two ORMs over one connection. |
| **Wolverine**, which has a Marten-native outbox | Would mean replacing MassTransit in Basket while Ordering keeps it - two messaging stacks in one system for one feature. |

## Consequences

**Positive**
- Checkout no longer depends on the broker, in success or in latency. Verified: with
  RabbitMQ stopped, checkout still returned 200 in under a second and the order
  arrived on its own once the broker came back.
- A double click produces one order. Verified with eight concurrent requests: one
  succeeded, the rest found the basket already gone, one order was created.
- The event cannot be lost after the basket is gone, and cannot be sent for a basket
  that was not cleared.

**Costs and caveats**
- Publishing is delayed by up to one polling interval (2s by default).
- Payment details now also sit at rest in BasketDb until the message is published,
  and indefinitely for a poisoned message. This makes taking card data out of
  `BasketCheckoutEvent` more urgent, not less.
- Every Basket.API instance runs its own relay. That is safe rather than merely
  tolerated - duplicates carry the same MessageId and the inbox keeps one - but two
  instances will do the same work. A claim or advisory lock would fix it if the
  waste ever matters.
- The outbox stores the event typed rather than in the shared
  `IntegrationEventEnvelope`, because `BasketCheckoutEvent` is not a versioned
  contract yet and the shared type registry requires one. It should move once it is.

## Implementation note

The relay's "which messages are due" query filters on a timestamp. With `DateTime`
properties Marten compares them as `timestamp without time zone`, which Npgsql
refuses to bind a UTC `DateTime` to - every relay cycle would have thrown and no
checkout would ever have been published, silently. The outbox document therefore
uses `DateTimeOffset`. This was found by running the tests against a real
PostgreSQL; fakes did not show it.

## Related

- ADR 0001 - Delivery guarantees: at-least-once and consumer idempotency
- ADR 0002 - Single CDC worker instance
