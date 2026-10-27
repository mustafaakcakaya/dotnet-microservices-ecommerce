using System.Text.Json.Serialization;
using BuildingBlocks.Messaging.Events.Basket.V1;

namespace Basket.API.Models;

/// <summary>
/// A user's basket. Lines are added, discounted and checked out only through its
/// methods, so a stored basket always holds valid lines.
/// </summary>
/// <remarks>
/// Setters are private and marked <see cref="JsonIncludeAttribute"/> so Marten and
/// the Redis cache can still read a stored basket back. The stored JSON keeps the
/// same shape as before: userName, items and the computed totalPrice.
/// </remarks>
public class ShoppingCart
{
    private List<ShoppingCartItem> _items = [];

    [JsonInclude] public string UserName { get; private set; } = default!;

    [JsonInclude]
    public IReadOnlyList<ShoppingCartItem> Items
    {
        get => _items;
        private set => _items = [.. value];
    }

    public decimal TotalPrice => _items.Sum(item => item.Price * item.Quantity);

    public ShoppingCart(string userName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userName);
        UserName = userName;
    }

    [JsonConstructor]
    private ShoppingCart()
    {
    }

    /// <summary>
    /// Adds a line, or adds to the quantity of the line with the same product and colour.
    /// </summary>
    public void AddItem(Guid productId, string productName, string color, int quantity, decimal price)
    {
        var existing = _items.FirstOrDefault(item => item.IsSameLine(productId, color));
        if (existing is not null)
        {
            existing.IncreaseQuantity(quantity);
            return;
        }

        _items.Add(ShoppingCartItem.Create(productId, productName, color, quantity, price));
    }

    /// <summary>
    /// Takes <paramref name="amount"/> off the unit price of every line of that product.
    /// Looking the discount up is the caller's job; how it applies is the basket's.
    /// </summary>
    public void ApplyDiscount(string productName, decimal amount)
    {
        foreach (var item in _items.Where(item => item.ProductName == productName))
        {
            item.ApplyDiscount(amount);
        }
    }

    /// <summary>
    /// Turns this basket, as it is stored, into the checkout that goes to Ordering.
    /// The lines and the total come from the basket, never from the request, so the
    /// order is built from what was actually in it. The draft only contributes what
    /// the basket does not know: customer, addresses and payment token.
    /// </summary>
    /// <param name="basketVersion">The stored version being checked out; it decides the checkout id.</param>
    /// <exception cref="EmptyBasketException">There is nothing to order.</exception>
    public BasketCheckoutSubmittedIntegrationEvent ToCheckout(
        BasketCheckoutSubmittedIntegrationEvent draft,
        Guid basketVersion,
        DateTimeOffset occurredOnUtc)
    {
        if (draft.UserName != UserName)
        {
            throw new InvalidOperationException(
                $"Checkout for {draft.UserName} cannot be made from the basket of {UserName}.");
        }

        if (_items.Count == 0)
        {
            throw new EmptyBasketException(UserName);
        }

        return draft with
        {
            Id = CheckoutId.For(UserName, basketVersion),
            OccurredOnUtc = occurredOnUtc.UtcDateTime,
            TotalPrice = TotalPrice,
            Items = _items
                .Select(item => new BasketCheckoutLine(item.ProductId, item.ProductName, item.Quantity, item.Price))
                .ToList()
        };
    }
}
