# ADR 0001 — Outbox + CDC teslimat garantisi: at-least-once ve consumer-side idempotency

- **Durum:** Kabul edildi
- **Tarih:** 2026-09-03
- **Kapsam:** Ordering servisi outbox akışı, Ordering.Worker (CDC listener), tüm consumer'lar

## Bağlam

Ordering servisi, aggregate değişikliğini ve dışarı yayınlanacak integration event'i aynı SQL
transaction'ında yazar (Transactional Outbox). Ayrı bir worker, SQL Server CDC üzerinden
`dbo.OutboxMessages` tablosuna gelen INSERT'leri okuyup message broker'a publish eder ve
başarılı publish sonrası CDC checkpoint'ini (son işlenen LSN) ilerletir.

Publish ile checkpoint yazımı iki ayrı işlemdir ve atomik değildir; broker'ın kendisi de
transaction'a dahil edilemez. Bu nedenle üç seçenek arasından birini seçmek zorundayız.

## Karar

**At-least-once teslimat benimsenir; duplicate'ler consumer tarafında idempotency (inbox
pattern) ile elenir.**

Bunun somut karşılığı:

1. Checkpoint **yalnızca** bir LSN grubu (tek kaynak transaction) tamamen publish edildikten
   sonra ilerler. Publish başarısızsa checkpoint ilerlemez, mesaj yeniden okunur.
2. Publish başarılı olup checkpoint yazılmadan worker çökerse, aynı mesaj tekrar yayınlanır.
   Bu **kabul edilen** bir durumdur; kayıp yerine tekrar tercih edilir.
3. Her integration event'in `Id` alanı broker `MessageId` olarak taşınır ve mesajın kimliği
   uçtan uca değişmez (outbox satırı → CDC kaydı → broker mesajı).
4. Consumer'lar bu `MessageId` üzerinden **inbox** tablosuna yazarak aynı mesajı ikinci kez
   işlemeyi reddeder. `BuildingBlocks.Messaging` bunun için hazır bir store ve MassTransit
   filtresi sunar; yeni bir consumer yazan herkes bunu kullanmak zorundadır.

## Değerlendirilen alternatifler

| Alternatif | Neden seçilmedi |
| --- | --- |
| **At-most-once** (önce checkpoint, sonra publish) | Publish öncesi çökme mesajı kalıcı olarak kaybettirir. Sipariş event'i kaybı iş açısından kabul edilemez. |
| **Exactly-once** (2PC / XA, broker'ı transaction'a dahil etme) | SQL Server + RabbitMQ/Kafka arasında dağıtık transaction operasyonel karmaşıklık ve performans maliyeti getirir; Kafka tarafında pratikte transactional producer + consumer offset yönetimi gerektirir. Bu ölçekte gereksiz. |
| **Dedup'ı broker'a bırakmak** | RabbitMQ'da yerleşik dedup yok; Kafka'nın idempotent producer'ı yalnızca producer retry'larını kapsar, worker restart'ını kapsamaz. |

## Sonuçları

**Olumlu**
- Mesaj kaybı yok; worker/broker/DB kesintileri sonrası akış kendiliğinden toparlanır.
- Publish tarafı basit kalır (dağıtık transaction yok), test edilebilirliği yüksektir.

**Bedeli / dikkat edilecekler**
- Her consumer idempotent olmak **zorundadır**; olmayan bir consumer duplicate iş üretir.
- Inbox tablosunun büyümesi için retention/temizlik gerekir.
- Event'ler aggregate başına sıralıdır (Kafka'da key = `AggregateId`), global sıra garantisi yoktur.
- Şu an **tek bir CDC listener instance'ı** çalıştırılır (bkz. ADR 0002).

## İlgili

- ADR 0002 — CDC worker tek instance kısıtı ve yatay ölçekleme planı
