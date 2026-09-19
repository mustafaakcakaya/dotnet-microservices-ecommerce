using BuildingBlocks.Messaging.Events;

namespace Basket.API.Data;

public interface IBasketRepository
{
    Task<ShoppingCart> GetBasket(string userName, CancellationToken cancellationToken = default);
    Task<ShoppingCart> StoreBasket(ShoppingCart basket, CancellationToken cancellationToken = default);
    Task<bool> DeleteBasket(string userName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks the basket out in one transaction: loads it from the database,
    /// deletes it and records the checkout event in the outbox. Nothing is
    /// published here - the outbox relay does that after the commit.
    /// Returns the event as stored, with its id and total filled in.
    /// </summary>
    /// <exception cref="Exceptions.BasketNotFoundException">There is no basket to check out.</exception>
    Task<BasketCheckoutEvent> CheckoutBasket(BasketCheckoutEvent draft, CancellationToken cancellationToken = default);
}
