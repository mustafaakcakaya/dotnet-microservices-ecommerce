using Basket.API.Outbox;
using BuildingBlocks.Messaging.Events.Basket.V1;

namespace Basket.API.Data;

public class BasketRepository(IDocumentSession session, TimeProvider timeProvider) : IBasketRepository
{
    public async Task<ShoppingCart> GetBasket(string userName, CancellationToken cancellationToken = default)
    {
        var basket = await session.LoadAsync<ShoppingCart>(userName, cancellationToken);

        return basket ?? throw new BasketNotFoundException(userName);
    }

    public async Task<ShoppingCart> StoreBasket(ShoppingCart basket, CancellationToken cancellationToken = default)
    {
        session.Store(basket);

        await session.SaveChangesAsync(cancellationToken);

        return basket;
    }

    public async Task<bool> DeleteBasket(string userName, CancellationToken cancellationToken = default)
    {
        session.Delete<ShoppingCart>(userName);

        await session.SaveChangesAsync(cancellationToken);

        return true;
    }

    public async Task<BasketCheckoutSubmittedIntegrationEvent> CheckoutBasket(
        BasketCheckoutSubmittedIntegrationEvent draft,
        CancellationToken cancellationToken = default)
    {
        // Always read from the database, never the cache: a stale cached basket
        // that was already checked out must not be checked out a second time.
        var basket = await session.LoadAsync<ShoppingCart>(draft.UserName, cancellationToken)
                     ?? throw new BasketNotFoundException(draft.UserName);

        // No metadata means a concurrent checkout deleted the basket after it was
        // loaded above. Falling back to an empty version would derive a different
        // id and record a second checkout of the same basket.
        var metadata = await session.MetadataForAsync(basket, cancellationToken)
                       ?? throw new BasketNotFoundException(draft.UserName);

        var now = timeProvider.GetUtcNow();
        var checkoutEvent = basket.ToCheckout(draft, metadata.CurrentVersion, now);

        session.Delete<ShoppingCart>(draft.UserName);
        session.Store(BasketOutboxMessage.For(checkoutEvent, draft.UserName, now));

        // One SaveChanges, one database transaction: the basket disappears if and
        // only if the event is recorded.
        await session.SaveChangesAsync(cancellationToken);

        return checkoutEvent;
    }
}
