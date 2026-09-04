# ADR 0002 — CDC worker şimdilik tek instance çalışır

- **Durum:** Kabul edildi (geçici kısıt — yatay ölçekleme ileride ele alınacak)
- **Tarih:** 2026-09-03
- **Kapsam:** Ordering.Worker

## Bağlam

`Ordering.Worker`, `dbo.OutboxCdcCheckpoints` tablosunda `ConsumerName` başına tek bir
"son işlenen LSN" tutar. Aynı `ConsumerName` ile iki instance aynı anda çalışırsa:

- İkisi de aynı checkpoint'ten okur, **aynı mesajları publish eder** (gereksiz duplicate),
- Checkpoint yazımında son yazan kazanır; yavaş instance checkpoint'i **geri alabilir**
  ve daha da fazla tekrar üretir.

Duplicate'ler consumer idempotency'si sayesinde veri bozulmasına yol açmaz (ADR 0001), ancak
gereksiz yük ve karışık log/metrik üretir.

## Karar

Şimdilik **yalnızca tek bir CDC listener instance'ı** çalıştırılır. Compose ve deployment
tanımları worker'ı tek replika olarak kurar; ölçekleme yatay değil, `BatchSize` /
`PollingInterval` ayarıyla dikey yapılır.

## İleride (planlanan iş)

Birden fazla instance isteneceği şimdiden biliniyor. Bunun için gereken minimum tasarım:

1. **Leader election / lease:** checkpoint tablosuna `LeaseOwner`, `LeaseExpiresOnUtc`
   kolonları eklenip her instance periyodik olarak lease yeniler; lease'i olmayan instance
   pasif bekler. (SQL Server'da `sp_getapplock` ile de yapılabilir; lease tablosu
   gözlemlenebilirlik açısından daha iyidir.)
2. Checkpoint yazımı `WHERE LeaseOwner = @Me AND LeaseExpiresOnUtc > SYSUTCDATETIME()`
   koşuluyla yapılıp geri alma (regression) engellenir.
3. Alternatif olarak `ConsumerName` başına partisyonlama (örn. `AggregateId` hash aralığı)
   ile gerçek paralellik; ancak CDC okuması LSN sıralı olduğu için bu daha karmaşıktır ve
   aggregate başına sıralamayı korumak için dikkat ister.

Bu iş **bu değişikliğin kapsamı dışındadır** ve ayrı bir ADR + implementasyon ile ele alınacaktır.

## İlgili

- ADR 0001 — At-least-once ve consumer-side idempotency
