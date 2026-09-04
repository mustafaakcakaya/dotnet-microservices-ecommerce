# dotnet-microservices-ecommerce

.NET 8 ile yazılmış, e-ticaret alanını dört servise bölen bir mikroservis örneği. Her servis
kendi verisine sahiptir ve problemine uygun kalıcılık teknolojisini kullanır; servisler
arası iletişim senkron (gRPC) ve asenkron (message broker) olmak üzere iki şekilde kurulur.

## Servisler

| Servis | Sorumluluk | Kalıcılık | Öne çıkan yapı |
| --- | --- | --- | --- |
| **Catalog.API** | Ürün kataloğu | PostgreSQL (Marten, document DB) | Vertical slice, Carter modülleri |
| **Basket.API** | Alışveriş sepeti | PostgreSQL (Marten) + Redis | Cache-aside decorator, Discount'a gRPC çağrısı |
| **Discount.Grpc** | İndirim kuponları | SQLite (EF Core) | gRPC servis |
| **Ordering** | Sipariş yaşam döngüsü | SQL Server (EF Core) | Clean Architecture + DDD, Outbox + CDC |
| **Ordering.Worker** | Outbox mesajlarını yayınlar | — | SQL Server CDC okuyucu, BackgroundService |

## Kullanılan mimari yaklaşımlar

**Clean Architecture (Ordering).** Bağımlılıklar dışarıdan içeriye doğrudur:
`Ordering.API → Ordering.Application → Ordering.Domain`, altyapı en dışta
(`Ordering.Infrastructure`). Domain katmanı hiçbir altyapı paketine bağlı değildir — ne EF
Core'u ne de message broker'ı tanır.

**Domain-Driven Design.** `Order` bir aggregate root'tur; `OrderId`, `Address`, `Payment`,
`OrderName` gibi value object'ler kimlik ve doğrulamayı domain içinde tutar. İş kuralları
entity'lerin içinde yaşar, servislere sızmaz. Aggregate, olan biteni **domain event** olarak
biriktirir.

**CQRS.** Komut ve sorgular ayrı tiplerdir (`ICommand`, `IQuery` — `BuildingBlocks/CQRS`),
MediatR ile karşılık gelen handler'a yönlendirilir. Kesişen ilgiler pipeline behaviour
olarak eklenir: `ValidationBehaviour` (FluentValidation) ve `LoggingBehaviour`.

**Vertical slice + Carter.** Her özellik kendi klasöründe endpoint, komut/sorgu, handler ve
validator ile birlikte durur. Katman katman değil, özellik özellik organizasyon.

**Transactional Outbox + Change Data Capture.** Ordering'in en kritik parçası. Aggregate
değişikliği ile dışarı yayınlanacak mesaj **aynı SQL transaction'ında** yazılır; broker'a
transaction içinde hiçbir çağrı yapılmaz. Ayrı bir worker, SQL Server CDC üzerinden outbox
tablosuna düşen kayıtları okuyup broker'a yayınlar. Detay aşağıda.

**Domain event ↔ integration event ayrımı.** Domain event içeride kalır (MediatR ile
in-process handler'lara gider). Dışarıya yalnızca açıkça tanımlanmış, versiyonlanmış
**integration event** çıkar (`BuildingBlocks.Messaging`). Domain veya EF entity'si asla
serialize edilmez; payload yalnızca diğer servislerin ihtiyaç duyduğu alanları taşır — kart
numarası, CVV gibi hassas ödeme verisi outbox'a, loglara ve broker'a **hiç girmez**.

## Ordering: outbox → CDC → broker akışı

```
Uygulama                 SQL Server                        Ordering.Worker
────────                 ──────────                        ───────────────
Order + OutboxMessage ─► dbo.Orders                        
   (TEK transaction)     dbo.OutboxMessages
                              │
                              │ CDC capture job (SQL Agent)
                              ▼
                         cdc.dbo_OutboxMessages_CT ──────► LSN > checkpoint olanları oku
                                                                  │
                                                                  ▼ publish (RabbitMQ | Kafka)
                                                                  │
                                                                  ▼ yalnızca başarılıysa
                                                           dbo.OutboxCdcCheckpoints
```

1. `Order.Create()` domain event üretir.
2. `DispatchDomainEventsInterceptor`, `SaveChanges` sırasında domain event'i integration
   event'e çevirip `OutboxMessage` olarak **aynı context'e** ekler → atomik yazım.
3. SQL Server CDC yalnızca `dbo.OutboxMessages` tablosunu izler (capture instance
   `dbo_OutboxMessages`).
4. Worker, change table'dan checkpoint'ten büyük LSN'leri okur ve yayınlar.
5. Checkpoint, bir LSN grubu tamamen yayınlandıktan **sonra** ilerler.

**Teslimat garantisi: at-least-once.** Publish başarılı olup checkpoint yazılmadan worker
çökerse mesaj tekrar yayınlanır. Bu bilinçli bir tercihtir — kayıp yerine tekrar. Bu yüzden
her consumer idempotent olmak zorundadır: integration event `Id`'si broker message id olarak
taşınır, consumer'lar `BuildingBlocks.Messaging.Inbox` ile bu id üzerinden tekrarları eler.
Gerekçe ve elenen alternatifler: [ADR 0001](docs/adr/0001-outbox-cdc-delivery-guarantees.md).

**Dayanıklılık.** Kontrollü exponential backoff + jitter; sürekli başarısız mesajlar
`MaxPublishAttempts` sonrası poison olarak işaretlenip atlanır, böylece tek bir bozuk mesaj
akışı süresiz kilitlemez. CDC retention penceresi aşılırsa worker sessizce atlamaz, kritik
hata üretir.

**Ölçekleme kısıtı.** Şu an tek worker instance'ı çalıştırılmalıdır; aynı `ConsumerName` ile
ikinci instance duplicate üretir ve checkpoint'i geri alabilir. Yatay ölçekleme için lease
tasarımı: [ADR 0002](docs/adr/0002-cdc-worker-single-instance.md).

## Message broker: RabbitMQ veya Kafka

Broker seçimi konfigürasyondandır, kod değişikliği gerektirmez:

```jsonc
"MessageBroker": {
  "Provider": "RabbitMq",   // veya "Kafka"
  "RabbitMq": { "Host": "amqp://localhost:5672", "UserName": "guest", "Password": "guest" },
  "Kafka":    { "BootstrapServers": "localhost:9092", "TopicPrefix": "" }
}
```

- **RabbitMQ** (MassTransit): kontratın exchange'ine yayınlanır, `MessageId` = event id.
- **Kafka** (Confluent): topic = event type (`ordering.order-created`), key = `AggregateId`
  (aggregate başına sıralama), zarf bilgisi header'larda, `EnableIdempotence` + `acks=all`.

Yayın tarafındaki tek broker-farkındalığı `IIntegrationEventPublisher`'ın iki
implementasyonundadır; üstteki hiçbir kod broker'ı tanımaz.

## Çalıştırma

```bash
cd src
docker compose up -d                      # RabbitMQ ile
docker compose --profile kafka up -d      # Kafka'yı da ayağa kaldırır
```

`orderdb` servisi `MSSQL_AGENT_ENABLED=true` ile çalışır — **CDC capture job'ı SQL Server
Agent olmadan çalışmaz**. CDC ve worker tabloları EF migration'ları tarafından oluşturulur
(`EnableOutboxCdc` migration'ı `suppressTransaction: true` kullanır, çünkü `sp_cdc_enable_db`
transaction içinde çalışmayı reddeder).

Servis adresleri: Catalog `6000`, Basket `6001`, Discount `6002`, Ordering.Worker `6003`,
RabbitMQ yönetim arayüzü `15672`, Kafka `29092`.

## Testler

```bash
dotnet test tests/Services/Ordering/Ordering.Domain.UnitTests
dotnet test tests/Services/Ordering/Ordering.Application.UnitTests
dotnet test tests/Services/Ordering/Ordering.Worker.UnitTests
dotnet test tests/Services/Ordering/Ordering.IntegrationTests   # Docker gerektirir
```

Integration testleri Testcontainers ile **gerçek SQL Server (CDC etkin)** ve **gerçek Kafka**
ayağa kaldırır; migration'ları sıfırdan uygular, order + outbox atomikliğini, CDC akışını,
broker hatasında checkpoint'in ilerlemediğini ve consumer idempotency'sini doğrular.

## Ortak kütüphaneler

- **BuildingBlocks** — CQRS arayüzleri, validation/logging behaviour'ları, exception handler,
  pagination.
- **BuildingBlocks.Messaging** — integration event kontratları ve versiyonlama, serializer,
  RabbitMQ/Kafka publisher'ları, inbox (idempotent consumer) altyapısı.

## Dokümanlar

- [ADR 0001 — Teslimat garantisi: at-least-once ve consumer idempotency](docs/adr/0001-outbox-cdc-delivery-guarantees.md)
- [ADR 0002 — CDC worker tek instance kısıtı](docs/adr/0002-cdc-worker-single-instance.md)
- [Ordering.Worker runbook](src/Services/Ordering/Ordering.Worker/README.md)
