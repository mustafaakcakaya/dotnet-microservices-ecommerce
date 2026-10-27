# ADR 0005 - Orders are identified by their id, not a name

- **Status:** Accepted
- **Date:** 2026-10-27
- **Scope:** Ordering domain, API and database; `ordering.order-created` and
  `ordering.order-updated` contracts; Shopping.Web order list

## Context

`Order` carried an `OrderName` value object that had to be exactly five characters long.
Nothing generated a name: the checkout consumer used the shopper's user name. A checkout
from any user whose name was not five characters long faulted in Ordering and ended up in
the error queue, so the order was never created.

The name added nothing an order did not already have. Every order has an `OrderId` (a GUID),
and it links to its shopper through `CustomerId`. The only reader of the name was
`GET /orders/{orderName}`, a substring search the storefront did not use; the storefront
lists a shopper's orders by customer id.

## Decision

**Remove `OrderName`. An order is identified by `OrderId` alone.**

1. The `OrderName` value object, the `Order.OrderName` property and the `Orders.OrderName`
   column are removed (`RemoveOrderName` migration). `OrderDto` no longer carries a name.
2. `GET /orders/{orderName}` is replaced by `GET /orders/{id}`, which returns one order or
   404.
3. Order lists are sorted newest first (`CreatedAt`, then id for a stable page order)
   instead of by name.
4. `ordering.order-created` and `ordering.order-updated` move to **schema version 2**
   without `orderName`. Version 1 was removed, not kept alongside: no service consumes these
   events, and the Ordering outbox held no unpublished v1 messages.
5. The storefront shows the order id where it showed the name.

## Alternatives considered

| Alternative | Why not |
| --- | --- |
| **Relax the length rule (for example up to 25 characters)** | Fixes the fault but keeps a field that is just a copy of the user name, plus a search endpoint nobody uses. |
| **Generate a human-friendly order number** | A real feature (sequence, format, uniqueness) with no current reader. It can be added when something needs it. |
| **Drop the field but keep schema version 1** | The contract would change without its version saying so. |

## Consequences

- A checkout succeeds for any user name.
- The migration is one-way: `Down` restores the column, not the names.
- Consumers of the order events, when there are any, must read version 2.
