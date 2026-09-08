# Implementation plan - Kafka support, CDC in EF migrations, consumer inbox

Date: 2026-09-03 · Status: implemented

Starting point: Ordering already had a Transactional Outbox with SQL Server CDC and a worker
publishing to RabbitMQ through MassTransit; CDC was set up by an idempotent initializer inside
the worker plus a separate SQL script.

This plan closed three gaps:

1. Make the broker a configuration choice so the services can **connect to Kafka when Kafka is
   there**.
2. Make CDC setup **part of the EF migrations** (a single source of truth).
3. Let consumers be **idempotent** (inbox pattern) - the obligation ADR 0001 creates.

---

## Phase 1 - Broker abstraction: RabbitMQ and Kafka

**Where:** `src/BuildingBlocks/BuildingBlocks.Messaging`

- Extracted a shared `IntegrationEventEnvelope` (Id, EventType, SchemaVersion, AggregateId,
  CorrelationId, OccurredOnUtc, Payload), replacing the worker's `OutboxMessageRecord` so the CDC
  reader and the publishers speak the same envelope.
- Moved `IIntegrationEventPublisher` out of the worker into BuildingBlocks.Messaging.
- Two implementations:
  - `RabbitMqIntegrationEventPublisher` - MassTransit `IBus.Publish`, `MessageId = envelope.Id`.
  - `KafkaIntegrationEventPublisher` - Confluent producer.
    - topic = `{TopicPrefix}{EventType}` (for example `ordering.order-created`)
    - key = `AggregateId`, giving **per-aggregate ordering**
    - headers: `message-id`, `event-type`, `schema-version`, `correlation-id`, `occurred-on-utc`
    - value = the stored JSON payload as-is, never re-serialized
    - `EnableIdempotence=true` and `Acks=All`, so producer retries are deduplicated broker-side
- `MessageBrokerOptions`: `Provider: RabbitMq | Kafka` plus provider-specific sections.
  `AddMessageBroker(configuration)` registers the matching publisher and health check.
- Broker knowledge is confined to those two classes; the worker only knows the interface.

## Phase 2 - CDC setup moved into EF migrations

**Where:** `src/Services/Ordering/Ordering.Infrastructure`

- The worker's tables were promoted to EF entities so the schema is owned in one place:
  `OutboxCdcCheckpoint`, `OutboxPublishFailure`, `InboxMessage`
  → the `AddOutboxCdcAndInboxTables` migration (ordinary `CreateTable` operations, snapshot
  consistent).
- CDC enablement is its own migration, `EnableOutboxCdc`.
  - Every statement runs with `migrationBuilder.Sql(script, suppressTransaction: true)`.
    **Why:** `sys.sp_cdc_enable_db` and `sp_cdc_enable_table` refuse to execute inside a
    transaction, and EF wraps migrations in one by default. `suppressTransaction` exists for
    exactly this case, and it is what allows CDC setup to live in a migration rather than a
    separate deployment script.
  - The script is idempotent and creates a capture instance for `dbo.OutboxMessages` only.
- The worker's `CdcSchemaInitializer`, its `ApplySchema` option and `Scripts/cdc-init.sql` were
  **deleted** - the schema belongs to the migrations, and two sources would drift.
  `CdcReadinessWaiter` replaced them: the worker waits until the capture instance exists and logs
  what it is waiting for, since it can start before the migrations have been applied.

## Phase 3 - Consumer-side idempotency (inbox)

**Where:** `src/BuildingBlocks/BuildingBlocks.Messaging/Inbox`

- `IInboxStore`: `TryBeginAsync(messageId, consumerName)` and `CompleteAsync(...)`.
- `SqlServerInboxStore` over `dbo.InboxMessages` (primary key: MessageId + ConsumerName).
  A first sighting inserts the row and returns true; a second one hits the primary key violation
  and is skipped.
- MassTransit integration: `IdempotentConsumeFilter<T>`. A message without a `MessageId` is
  rejected, because it cannot be deduplicated.
- The same store works for Kafka consumers - the interface is broker-agnostic.

## Phase 4 - Compose and configuration

- Kafka lives behind a **`kafka` profile** so the default stack stays light:
  `docker compose --profile kafka up`.
- `MessageBroker__Provider` is an environment variable on the worker (default RabbitMq).
- `MSSQL_AGENT_ENABLED=true` on `orderdb` - without SQL Server Agent the CDC capture job never
  runs, which was the missing piece that made the whole pipeline look broken.
- No passwords or connection strings in source; they come from compose environment variables.

## Phase 5 - Tests

- Existing unit tests updated for the new envelope type.
- New: Kafka publisher integration test (Testcontainers) - the message really is produced, with
  the right key and `message-id` header.
- New: inbox idempotency test against a real SQL Server - the same `MessageId` delivered twice
  runs the handler once.
- Existing CDC pipeline and outbox transaction tests pass unchanged.
- End to end on Docker, verified separately for the RabbitMQ and Kafka profiles.

## Deliberately out of scope

- **Horizontal scaling of the worker** - it stays single-instance; the lease design is in ADR 0002.
- Retention and archival for the outbox and inbox tables.
- A schema registry (Avro/Protobuf); JSON plus the `SchemaVersion` field is enough for now.

## Acceptance criteria

- [x] With `MessageBroker:Provider=Kafka` the worker publishes to Kafka, no code change required.
- [x] After a clean `dotnet ef database update` CDC is ready; the worker sets nothing up itself.
- [x] The same `MessageId` delivered twice runs consumer logic once.
- [x] All test suites green; no new build warnings.
