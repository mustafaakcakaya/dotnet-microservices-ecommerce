# Uygulama planı — Kafka desteği, CDC'nin EF migration'a taşınması, consumer inbox'ı

Tarih: 2026-09-03 · Durum: uygulanıyor

Mevcut durum: Ordering'de Transactional Outbox + SQL Server CDC + RabbitMQ(MassTransit)
publish eden `Ordering.Worker` çalışıyor; CDC kurulumu worker içindeki idempotent bir
initializer + ayrı SQL script ile yapılıyor.

Bu plan üç boşluğu kapatır:

1. Broker seçimi konfigürasyona bağlansın; **Kafka varsa sorunsuz Kafka'ya bağlanabilsin**.
2. CDC kurulumu **EF migration'ının parçası** olsun (tek kaynak: migration).
3. Consumer tarafı **idempotent** olabilsin (inbox pattern) — ADR 0001'in gereği.

---

## Faz 1 — Broker abstraction: RabbitMQ + Kafka

**Nerede:** `src/BuildingBlocks/BuildingBlocks.Messaging`

- `IntegrationEventEnvelope` (Id, EventType, SchemaVersion, AggregateId, CorrelationId,
  OccurredOnUtc, Payload) ortak tipe çıkarılır. Worker'daki `OutboxMessageRecord` bununla
  değiştirilir — CDC okuyucusu ile publisher aynı zarfı konuşur.
- `IIntegrationEventPublisher` worker'dan BuildingBlocks.Messaging'e taşınır.
- İki implementasyon:
  - `RabbitMqIntegrationEventPublisher` — MassTransit `IBus.Publish`, `MessageId = envelope.Id`.
  - `KafkaIntegrationEventPublisher` — `Confluent.Kafka` producer.
    - topic = `{TopicPrefix}{EventType}` (örn. `ordering.order-created`)
    - key = `AggregateId` → **aggregate başına sıralama** garantisi
    - headers: `message-id`, `event-type`, `schema-version`, `correlation-id`, `occurred-on-utc`
    - value = outbox'taki JSON payload aynen (yeniden serialize edilmez)
    - `EnableIdempotence=true`, `Acks=All` → producer retry'larında broker-side dedup
- `MessageBrokerOptions`: `Provider: RabbitMq | Kafka` + provider'a özel alt bölümler.
  `AddMessageBroker(configuration)` provider'a göre publisher + health check kaydeder.
- Publisher'ın broker bağımlılığı bu iki sınıfla sınırlı kalır; worker sadece arayüzü bilir.

## Faz 2 — CDC kurulumu EF migration'a

**Nerede:** `src/Services/Ordering/Ordering.Infrastructure`

- Worker'ın kullandığı tablolar EF entity'sine terfi eder (şema tek yerden yönetilsin):
  `OutboxCdcCheckpoint`, `OutboxPublishFailure`, `InboxMessage`.
  → `AddOutboxCdcTables` migration'ı (normal `CreateTable` operasyonları, snapshot tutarlı).
- CDC etkinleştirme ayrı migration: `EnableOutboxCdc`.
  - `migrationBuilder.Sql(script, suppressTransaction: true)` kullanılır. **Neden:**
    `sys.sp_cdc_enable_db` / `sp_cdc_enable_table` transaction içinde çalışmayı reddeder;
    EF migration'ları varsayılan olarak transaction içinde koşar. `suppressTransaction`
    tam olarak bu durum için vardır — böylece CDC kurulumu migration'ın parçası olur.
  - Script idempotenttir (tekrar çalıştırılabilir), yalnız `dbo.OutboxMessages` için
    `dbo_OutboxMessages` capture instance'ı açar.
- Worker'daki `CdcSchemaInitializer`, `ApplySchema` option'ı ve `Scripts/cdc-init.sql`
  **silinir** (şema artık migration'ın; iki kaynak drift üretir).
  Yerine `CdcReadinessWaiter`: worker başlarken capture instance hazır olana kadar bekler ve
  neden beklediğini açıkça loglar (worker, API migration'ları uygulamadan önce ayağa kalkabilir).

## Faz 3 — Consumer-side idempotency (inbox)

**Nerede:** `src/BuildingBlocks/BuildingBlocks.Messaging/Inbox`

- `IInboxStore`: `TryBeginAsync(messageId, consumerName, ct) -> bool`, `CompleteAsync(...)`.
- `SqlServerInboxStore`: `dbo.InboxMessages` (PK: MessageId + ConsumerName).
  İlk görülüşte satır eklenir → `true`; ikinci görülüşte PK ihlali yakalanır → `false` (atla).
- MassTransit entegrasyonu: `IdempotentConsumeFilter<T>` + `UseIdempotentConsumers()`.
  `MessageId` yoksa mesaj reddedilir (idempotency anahtarsız işlenemez).
- Kafka consumer'ları için de aynı store kullanılabilir (broker-agnostik arayüz).

## Faz 4 — Compose ve konfigürasyon

- `compose.yaml`: Kafka (KRaft modu, tek broker) **`kafka` profili** altında — varsayılan
  stack'i ağırlaştırmaz, `docker compose --profile kafka up` ile gelir.
- `ordering.worker` servisine `MessageBroker__Provider` env'i eklenir (varsayılan RabbitMq).
- Şifre/connection string kaynak koda gömülmez; compose env'den okunur.

## Faz 5 — Testler

- Mevcut unit testler yeni zarf tipine göre güncellenir.
- Yeni: Kafka publisher integration testi (Testcontainers.Kafka) — mesaj gerçekten üretiliyor,
  `message-id` header'ı ve key doğru.
- Yeni: inbox idempotency testi (gerçek SQL Server) — aynı `MessageId` iki kez tüketilir,
  handler bir kez çalışır.
- Mevcut CDC pipeline + outbox transaction testleri regresyonsuz geçmeli.
- Docker'da uçtan uca: RabbitMQ profili ve Kafka profili ile ayrı ayrı doğrulama.

## Kapsam dışı (bilinçli)

- **Worker'ın yatay ölçeklenmesi** — tek instance kalır, lease/lock tasarımı ADR 0002'de.
- Outbox/inbox retention & arşivleme job'ı.
- Schema registry (Avro/Protobuf); şimdilik JSON + `SchemaVersion` alanı yeterli.

## Kabul kriterleri

- [ ] `MessageBroker:Provider=Kafka` ile worker Kafka'ya publish eder, kod değişikliği gerekmez.
- [ ] Sıfırdan `dotnet ef database update` sonrası CDC hazır; worker ek kurulum yapmaz.
- [ ] Aynı `MessageId` iki kez teslim edilse de consumer iş mantığı bir kez çalışır.
- [ ] Tüm test paketleri yeşil; build uyarısı artmaz.
