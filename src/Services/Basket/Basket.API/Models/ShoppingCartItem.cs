using System.Text.Json.Serialization;

namespace Basket.API.Models;

/// <summary>
/// A line of a basket. Changed only through <see cref="ShoppingCart"/>, which owns it.
/// </summary>
/// <remarks>
/// Setters are private and marked <see cref="JsonIncludeAttribute"/> so Marten and
/// the Redis cache can still read a stored line back.
/// </remarks>
public class ShoppingCartItem
{
    [JsonInclude] public int Quantity { get; private set; }
    [JsonInclude] public string Color { get; private set; } = default!;
    [JsonInclude] public decimal Price { get; private set; }
    [JsonInclude] public Guid ProductId { get; private set; }
    [JsonInclude] public string ProductName { get; private set; } = default!;

    [JsonConstructor]
    private ShoppingCartItem()
    {
    }

    internal static ShoppingCartItem Create(Guid productId, string productName, string color, int quantity, decimal price)
    {
        if (productId == Guid.Empty)
        {
            throw new ArgumentException("A basket line must refer to a product.", nameof(productId));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(productName);
        ArgumentException.ThrowIfNullOrWhiteSpace(color);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(quantity);
        ArgumentOutOfRangeException.ThrowIfNegative(price);

        return new ShoppingCartItem
        {
            ProductId = productId,
            ProductName = productName,
            Color = color,
            Quantity = quantity,
            Price = price
        };
    }

    internal bool IsSameLine(Guid productId, string color) =>
        ProductId == productId && string.Equals(Color, color, StringComparison.OrdinalIgnoreCase);

    internal void IncreaseQuantity(int quantity)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(quantity);
        Quantity += quantity;
    }

    /// <summary>A discount larger than the price makes the line free, never negative.</summary>
    internal void ApplyDiscount(decimal amount)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(amount);
        Price = Math.Max(0, Price - amount);
    }
}
