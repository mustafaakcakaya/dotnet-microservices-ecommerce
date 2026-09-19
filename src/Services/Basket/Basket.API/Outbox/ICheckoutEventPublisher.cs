using BuildingBlocks.Messaging.Events;
using MassTransit;

namespace Basket.API.Outbox;

/// <summary>
/// Publishes a stored checkout event. Kept separate from the relay so the relay's
/// retry and poison logic can be tested without a broker.
/// </summary>
public interface ICheckoutEventPublisher
{
    Task PublishAsync(BasketCheckoutEvent checkoutEvent, CancellationToken cancellationToken);
}

/// <summary>
/// Publishes through MassTransit, because Ordering consumes the checkout with a
/// MassTransit consumer. The outbox id travels as the MessageId: a message the
/// relay publishes twice (published, then crashed before deleting the row) keeps
/// its id, and Ordering's inbox drops the second copy.
/// </summary>
public sealed class MassTransitCheckoutEventPublisher(IBus bus) : ICheckoutEventPublisher
{
    public Task PublishAsync(BasketCheckoutEvent checkoutEvent, CancellationToken cancellationToken) =>
        bus.Publish(checkoutEvent, context => context.MessageId = checkoutEvent.Id, cancellationToken);
}
