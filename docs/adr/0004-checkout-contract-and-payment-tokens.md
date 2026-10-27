# ADR 0004 - Versioned checkout contract, real basket lines and payment tokens

- **Status:** Accepted
- **Date:** 2026-10-26
- **Scope:** `BuildingBlocks.Messaging` checkout contract, Basket.API checkout and outbox,
  Ordering's checkout consumer and `Payment` value object, new Payment.API, Shopping.Web checkout

## Context

After ADR 0003 the checkout no longer got lost, but what it carried was still wrong in three
ways, all in the same message:

1. **`BasketCheckoutEvent` was not a versioned contract.** It had no
   `[IntegrationEventContract]`, so the shared type registry could not resolve it. Basket's
   outbox therefore stored it typed and published it with its own MassTransit publisher, a
   second publishing path next to the envelope-based one Ordering uses.
2. **It carried the card in clear text** - number, expiry and CVV - over the broker, in
   BasketDb's outbox until published (indefinitely for a poisoned message), and from there into
   `dbo.Orders`, which returned it through the API.
3. **Ordering ignored the basket.** The consumer wrote the same two hard-coded products into
   every order; the basket's content never reached Ordering. Using the real lines exposed a
   further gap: Catalog derives product ids from product names, while Ordering's `Products`
   table holds four unrelated seed ids, and `OrderItems` has a foreign key to it - so an order
   for any real catalog product would have been rejected.

All three change the message, so they were done as one contract change.

## Decision

**One versioned contract, `basket.checkout-submitted` v1, carrying the basket's lines and a
payment token instead of the card. The card is held by a new Payment service.**

1. `BasketCheckoutSubmittedIntegrationEvent`
   (`BuildingBlocks.Messaging/Events/Basket/V1`), with
   `[IntegrationEventContract("basket.checkout-submitted", schemaVersion: 1)]`:
   user, customer, total, `Items` (product id, name, quantity, unit price), shipping and
   billing address, and `Payment` (token, last four digits, method). No card data.
2. **Basket's outbox stores the shared `IntegrationEventEnvelope`** - wire name, version,
   aggregate id (the user name) and JSON payload - built by `IntegrationEventEnvelope.From`.
   The relay publishes through the shared `IIntegrationEventPublisher`, which resolves the
   contract through the type registry exactly as the CDC worker does. Basket's own
   MassTransit publisher is gone. Everything ADR 0003 decided about the relay stands.
3. **The lines and the total come from the stored basket**, read in the checkout
   transaction, never from the request. An empty basket is rejected.
4. **Ordering builds the order from the lines** and recomputes the total. A total that does
   not match its lines faults the message; it is not repaired.
5. **Unknown products are added from the checkout.** The lines travel with
   `CreateOrderCommand` as product snapshots (id, name, price). Products Ordering does not
   hold yet are inserted in the same transaction as the order; known products keep their
   stored name and price.
6. **Payment.API holds the card.** `POST /payment/tokens` validates the card (Luhn, MM/YY not
   expired, 3-4 digit CVV), stores number, expiry and holder name in its own PostgreSQL
   (`PaymentDb`), and returns a random token (`pt_` + 128 random bits) and the last four
   digits. **The CVV is checked for shape and discarded - never stored, logged or returned.**
   Validation messages never echo the submitted value.
7. **Shopping.Web tokenizes first**, then checks out with the token. The card leaves the
   storefront only towards the Payment service; Basket and Ordering never receive it.
8. **Ordering keeps only the token.** `Payment` is now token, last four digits and method.
   The `ReplaceCardWithPaymentToken` migration adds the new columns, keeps existing orders'
   last four digits and gives them a `legacy-<order id>` token, then drops the card columns.
   `OrderDto` therefore no longer returns card data either.
9. **Hard cut-over, no compatibility window.** The old event type was removed rather than
   kept alongside the new one. At the moment of the change Basket's outbox held no messages
   and the checkout queue was empty; the three old-format messages in
   `basket-checkout-event-handler_error` were purged unread, since they carried card data and
   no consumer could read them any more.

## Alternatives considered

| Alternative | Why not |
| --- | --- |
| **Do the three items separately** | Each one changes the same message; three consecutive breaking changes to one contract buy nothing. |
| **Keep the old event alongside v1 for a while** | Nothing was in flight, so a compatibility layer would have been code with no reader. |
| **Token vault inside Basket** | Less work, but then Basket stores cards, and the next service that needs a card would have to call Basket for it. Payment details belong to a service whose only job is them. |
| **Drop the `OrderItems` -> `Products` foreign key** | Hides the gap instead of closing it; order items could then point at nothing. |
| **Replicate the catalog into Ordering via Catalog events** | The right shape eventually, but Catalog publishes no events today. Snapshots from the checkout close the gap now and leave that door open. |
| **Trust the total from the request or the event** | The basket is the source of truth; a mismatch means two services disagree about the order, which should be visible, not averaged away. |

## Consequences

**Positive**
- No card number or CVV in any message, outbox, log, BasketDb or OrderDb. The only place a
  card number is stored is PaymentDb.
- Orders contain what the shopper bought.
- One way of storing and publishing integration events across Basket and Ordering.
- A latent race in the checkout was found and fixed on the way: a request that loaded the
  basket just before a concurrent checkout deleted it fell back to an empty version and
  derived a *different* checkout id - two orders from one basket. A missing version now
  means the basket is gone (`BasketNotFoundException`).

**Costs and caveats**
- **PaymentDb stores card numbers in plain text.** Real systems encrypt them (and keep the
  key outside the database) or hand the card to a payment provider's vault. Acceptable for a
  sample that never sees real cards; not for anything else.
- Payment.API has no authentication, like Ordering.API, where that was deliberately left for
  later. Anyone who can reach it can create tokens; nothing reads cards back by token yet.
- `CardLast4` reaches Basket from the storefront, which got it from Payment. Basket does
  not verify the token with Payment; a wrong value would only affect what is displayed.
  Verification belongs where the payment is actually taken.
- Two concurrent orders introducing the same unknown product race on its insert; the loser
  fails and is retried by redelivery, by which time the product exists.
- Snapshot products keep the name and price they had on first sight. Ordering's product
  data is a reference copy, not the catalog.
- The migration is one-way: `Down` restores the columns' shape, not the card data.
- Basket still refuses to start with Kafka as the provider (ADR 0003); the checkout hand-off
  is RabbitMQ only.

## Related

- ADR 0001 - Delivery guarantees: at-least-once and consumer idempotency
- ADR 0003 - Checkout published through a transactional outbox
