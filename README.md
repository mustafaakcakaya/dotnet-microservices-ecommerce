# dotnet-microservices-ecommerce

A .NET 8 microservices sample that splits an e-commerce domain across four services.
Each service owns its data and uses the persistence technology that fits its problem;
services talk to each other both synchronously (gRPC) and asynchronously (message broker).

## Services

| Service | Responsibility | Persistence | Notable |
| --- | --- | --- | --- |
| **Catalog.API** | Product catalog | PostgreSQL (Marten, document DB) | Vertical slices, Carter modules |
| **Basket.API** | Shopping basket | PostgreSQL (Marten) + Redis | Cache-aside decorator, gRPC call to Discount |
| **Discount.Grpc** | Discount coupons | SQLite (EF Core) | gRPC service |
| **Ordering** | Order lifecycle | SQL Server (EF Core) | Clean Architecture + DDD, outbox + CDC |
| **Ordering.Worker** | Publishes outbox messages | — | SQL Server CDC reader, BackgroundService |

## Architectural approaches

**Clean Architecture (Ordering).** Dependencies point inwards:
`Ordering.API → Ordering.Application → Ordering.Domain`, with infrastructure on the outside
(`Ordering.Infrastructure`). The domain layer depends on no infrastructure package - it knows
neither EF Core nor the message broker.

**Domain-Driven Design.** `Order` is an aggregate root; value objects such as `OrderId`,
`Address`, `Payment` and `OrderName` keep identity and validation inside the domain. Business
rules live in the entities rather than leaking into services. The aggregate records what
happened as **domain events**.

**CQRS.** Commands and queries are distinct types (`ICommand`, `IQuery` in
`BuildingBlocks/CQRS`) dispatched to their handler by MediatR. Cross-cutting concerns are
pipeline behaviours: `ValidationBehaviour` (FluentValidation) and `LoggingBehaviour`.

**Vertical slices with Carter.** Each feature keeps its endpoint, command or query, handler and
validator together in one folder - organised by feature rather than by layer.

**Transactional Outbox with Change Data Capture.** The critical part of Ordering. The aggregate
change and the message to be published are written in the **same SQL transaction**; no broker
call happens inside that transaction. A separate worker reads the committed outbox rows through
SQL Server CDC and publishes them. Details below.

**Domain events versus integration events.** Domain events stay inside the service, dispatched
in-process through MediatR. Only explicitly defined, versioned **integration events** leave it
(`BuildingBlocks.Messaging`). Domain and EF entities are never serialized; a payload carries
only the fields other services actually need, and sensitive payment data - card number, CVV -
never reaches the outbox, the logs or the broker.

## Ordering: outbox to CDC to broker

```
Application              SQL Server                        Ordering.Worker
───────────              ──────────
Order + OutboxMessage ─► dbo.Orders
   (ONE transaction)     dbo.OutboxMessages
                              │
                              │ CDC capture job (SQL Agent)
                              ▼
                         cdc.dbo_OutboxMessages_CT ──────► read LSN > checkpoint
                                                                  │
                                                                  ▼ publish (RabbitMQ | Kafka)
                                                                  │
                                                                  ▼ only on success
                                                           dbo.OutboxCdcCheckpoints
```

1. `Order.Create()` raises a domain event.
2. During `SaveChanges`, `DispatchDomainEventsInterceptor` maps it to an integration event and
   adds an `OutboxMessage` to the **same context**, making the write atomic.
3. SQL Server CDC tracks only `dbo.OutboxMessages` (capture instance `dbo_OutboxMessages`).
4. The worker reads change-table rows with an LSN above its checkpoint and publishes them.
5. The checkpoint advances only **after** an LSN group has been published in full.

**Delivery is at-least-once.** If the worker dies after publishing but before writing the
checkpoint, the message is published again. That is a deliberate choice - redelivery over loss -
so every consumer must be idempotent: the integration event `Id` travels as the broker message
id, and consumers deduplicate on it using `BuildingBlocks.Messaging.Inbox`. Reasoning and the
rejected alternatives: [ADR 0001](docs/adr/0001-outbox-cdc-delivery-guarantees.md).

**Resilience.** Controlled exponential backoff with jitter; a message that keeps failing is
marked poisoned after `MaxPublishAttempts` and skipped, so one broken message cannot block the
stream indefinitely. If the CDC retention window is exceeded the worker raises a critical error
rather than skipping silently.

**Scaling constraint.** Exactly one worker instance may run today; a second one sharing the same
`ConsumerName` would produce duplicates and could move the checkpoint backwards. The lease design
needed for horizontal scaling: [ADR 0002](docs/adr/0002-cdc-worker-single-instance.md).

## Message broker: RabbitMQ or Kafka

The broker is chosen by configuration, with no code change:

```jsonc
"MessageBroker": {
  "Provider": "RabbitMq",   // or "Kafka"
  "RabbitMq": { "Host": "amqp://localhost:5672", "UserName": "guest", "Password": "guest" },
  "Kafka":    { "BootstrapServers": "localhost:9092", "TopicPrefix": "" }
}
```

- **RabbitMQ** (MassTransit): published to the contract's exchange, `MessageId` = event id.
- **Kafka** (Confluent): topic = event type (`ordering.order-created`), key = `AggregateId` so
  events of one aggregate stay ordered, envelope metadata in headers, `EnableIdempotence` and
  `acks=all` enabled.

The only broker-aware code is the two implementations of `IIntegrationEventPublisher`; nothing
above that layer knows which broker is in use.

## Running

```bash
cd src
docker compose up -d                      # with RabbitMQ
docker compose --profile kafka up -d      # also brings up Kafka
```

The `orderdb` service runs with `MSSQL_AGENT_ENABLED=true` - **the CDC capture job does not run
without SQL Server Agent**. CDC and the worker's tables are created by EF migrations; the
`EnableOutboxCdc` migration uses `suppressTransaction: true`, because `sp_cdc_enable_db` refuses
to run inside a transaction.

Ports: Catalog `6000`, Basket `6001`, Discount `6002`, Ordering.Worker `6003`,
RabbitMQ management UI `15672`, Kafka `29092`.

## Tests

```bash
dotnet test tests/BuildingBlocks/BuildingBlocks.UnitTests
dotnet test tests/Services/Basket/Basket.API.UnitTests
dotnet test tests/Services/Ordering/Ordering.Domain.UnitTests
dotnet test tests/Services/Ordering/Ordering.Application.UnitTests
dotnet test tests/Services/Ordering/Ordering.Worker.UnitTests
dotnet test tests/Services/Ordering/Ordering.IntegrationTests   # requires Docker
```

The integration tests start a **real SQL Server with CDC enabled** and a **real Kafka** through
Testcontainers. They apply the migrations from scratch and verify that an order and its outbox
row commit atomically, that the CDC pipeline delivers, that the checkpoint does not advance when
the broker fails, and that consumers are idempotent.

## Shared libraries

- **BuildingBlocks** - CQRS abstractions, validation and logging behaviours, exception handler,
  pagination.
- **BuildingBlocks.Messaging** - integration event contracts and versioning, serializer, RabbitMQ
  and Kafka publishers, inbox (idempotent consumer) support.

## Documents

- [ADR 0001 - Delivery guarantees: at-least-once and consumer idempotency](docs/adr/0001-outbox-cdc-delivery-guarantees.md)
- [ADR 0002 - Single CDC worker instance](docs/adr/0002-cdc-worker-single-instance.md)
- [Ordering.Worker runbook](src/Services/Ordering/Ordering.Worker/README.md)
