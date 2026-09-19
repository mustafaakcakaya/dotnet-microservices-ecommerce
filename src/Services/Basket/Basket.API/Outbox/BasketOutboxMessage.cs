using BuildingBlocks.Messaging.Events;

namespace Basket.API.Outbox;

/// <summary>
/// A checkout event waiting to be published. It is stored in the same Marten
/// transaction that deletes the basket, so a checkout either happens completely -
/// basket gone and event guaranteed to go out - or not at all. Publishing it is
/// the relay's job, never the request's.
/// </summary>
/// <remarks>
/// Timestamps are DateTimeOffset on purpose. The relay filters on them, and a
/// DateTime property is compared as "timestamp without time zone", which Npgsql
/// refuses to bind a UTC DateTime to - every relay cycle would fail and no
/// checkout would ever be published.
///
/// The event is stored typed rather than as the envelope Ordering's outbox uses,
/// because BasketCheckoutEvent is not a versioned contract yet and the shared
/// type registry needs one. Once it is, this can move to IntegrationEventEnvelope.
/// </remarks>
public class BasketOutboxMessage
{
    /// <summary>The checkout event id; also the broker MessageId.</summary>
    public Guid Id { get; set; }

    public BasketCheckoutEvent Message { get; set; } = default!;

    public DateTimeOffset OccurredOnUtc { get; set; }

    /// <summary>When the relay may next try to publish it.</summary>
    public DateTimeOffset NextAttemptOnUtc { get; set; }

    public int Attempts { get; set; }

    public string? LastError { get; set; }

    /// <summary>Set once the message has failed too often; the relay then leaves it alone.</summary>
    public DateTimeOffset? PoisonedOnUtc { get; set; }

    public static BasketOutboxMessage For(BasketCheckoutEvent checkoutEvent, DateTimeOffset nowUtc) => new()
    {
        Id = checkoutEvent.Id,
        Message = checkoutEvent,
        OccurredOnUtc = nowUtc,
        NextAttemptOnUtc = nowUtc
    };
}
