# Ordering.Worker — Transactional Outbox + SQL Server CDC

Reads committed outbox rows through SQL Server CDC and publishes them to the configured
message broker (RabbitMQ or Kafka).

## Flow

1. A domain event is raised on the `Order` aggregate (Domain layer, no broker dependency).
2. `DispatchDomainEventsInterceptor` (Ordering.Infrastructure) maps the domain event to a
   versioned integration event (`BuildingBlocks.Messaging`) and stages an `OutboxMessage`
   row on the same `DbContext`, so the order and the outbox row commit in **one SQL
   transaction**. No broker call happens inside the transaction.
3. SQL Server CDC tracks **only** `dbo.OutboxMessages` (capture instance `dbo_OutboxMessages`).
4. This worker polls the CDC change table for new INSERTs and publishes each envelope
   through `IIntegrationEventPublisher` (outbox `Id` = broker message id).
5. The LSN checkpoint (`dbo.OutboxCdcCheckpoints`) advances only after an LSN group is
   fully published.
6. If publishing fails the checkpoint is not advanced; the message is retried with
   exponential backoff. After `OutboxCdc:MaxPublishAttempts` failures it is marked
   poisoned (`dbo.OutboxPublishFailures.PoisonedOnUtc`) and skipped so the stream is
   never blocked forever.

## Delivery semantics

Delivery is **at-least-once**: a crash after publish but before the checkpoint write
re-delivers the message. Consumers MUST be idempotent — use
`BuildingBlocks.Messaging.Inbox` (`AddSqlServerInbox` + `IdempotentConsumeFilter`), which
deduplicates on the broker message id. Rationale and alternatives:
`docs/adr/0001-outbox-cdc-delivery-guarantees.md`.

## Choosing a broker

`MessageBroker:Provider` selects the transport — no code change is needed:

```jsonc
"MessageBroker": {
  "Provider": "RabbitMq",           // or "Kafka"
  "RabbitMq": { "Host": "amqp://localhost:5672", "UserName": "guest", "Password": "guest" },
  "Kafka":    { "BootstrapServers": "localhost:9092", "TopicPrefix": "" }
}
```

- **RabbitMQ** (MassTransit): published to the contract's exchange; `MessageId` is the event id.
- **Kafka** (Confluent): topic = `{TopicPrefix}{EventType}` (e.g. `ordering.order-created`),
  key = `AggregateId` so events of one aggregate stay ordered, envelope metadata in headers
  (`message-id`, `event-type`, `schema-version`, `correlation-id`, `occurred-on-utc`), and the
  stored JSON payload as the value. `EnableIdempotence` + `acks=all` are on by default.

Locally: `docker compose --profile kafka up` then set `MESSAGE_BROKER_PROVIDER=Kafka`.

## Schema and CDC setup

CDC and the worker's tables are created by the **Ordering EF migrations** — the worker
creates nothing at runtime:

- `AddOutboxMessages` — the outbox table.
- `AddOutboxCdcAndInboxTables` — `OutboxCdcCheckpoints`, `OutboxPublishFailures`, `InboxMessages`.
- `EnableOutboxCdc` — `sp_cdc_enable_db` + capture instance for `dbo.OutboxMessages`, executed
  with `suppressTransaction: true` because those procedures refuse to run inside the
  transaction EF wraps around migrations. Idempotent, so re-running is safe.

On startup the worker waits (`CdcReadinessWaiter`) until the capture instance exists and logs
what it is waiting for, since it may boot before the migrations have been applied.

## Operational notes

- **Edition**: CDC needs SQL Server Standard/Enterprise/Developer. The compose image
  `mcr.microsoft.com/mssql/server` defaults to Developer edition, which supports CDC.
- **SQL Server Agent** must run (`MSSQL_AGENT_ENABLED=true` for containers), otherwise the
  CDC capture job never copies changes and the worker never sees any row.
- **Single instance**: run exactly one worker per `ConsumerName`. Two instances would
  republish each other's messages and can move the checkpoint backwards. Horizontal scaling
  needs a lease — see `docs/adr/0002-cdc-worker-single-instance.md`.
- **Enablement gap**: CDC only captures changes made *after* the capture instance exists.
  Because `EnableOutboxCdc` runs as part of the migrations, this is covered — but never
  insert outbox rows into a database whose migrations have not been applied.
- **Checkpoint expired**: if the checkpoint falls behind the CDC retention window (default
  3 days), the worker logs a **critical** error and keeps failing instead of silently
  skipping. Recovery: compare `dbo.OutboxMessages` against what consumers received,
  re-publish the gap if needed, then reset the checkpoint:
  `DELETE FROM dbo.OutboxCdcCheckpoints WHERE ConsumerName = 'ordering-outbox-worker';`
- **Poison messages**: inspect `dbo.OutboxPublishFailures` joined to `dbo.OutboxMessages`;
  to replay one, clear its `PoisonedOnUtc` **and** move the checkpoint back. Poisoning is
  logged with the message id, never with the payload.
- **Health**: `GET /health` covers the SQL connection and the broker connection
  (`masstransit-bus` for RabbitMQ, `kafka` metadata check for Kafka).

## Configuration (`OutboxCdc` section)

| Key | Default | Meaning |
| --- | --- | --- |
| `BatchSize` | 100 | Max messages read from CDC per cycle |
| `PollingInterval` | 00:00:05 | Idle poll delay |
| `MaxPublishAttempts` | 10 | Attempts before a message is poisoned |
| `RetryBaseDelay` / `RetryMaxDelay` | 1s / 1m | Exponential backoff bounds |
| `ConsumerName` | ordering-outbox-worker | Checkpoint owner (one instance per name) |
