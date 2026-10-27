namespace Basket.API.Dtos;

/// <summary>A basket as the API returns it.</summary>
public record ShoppingCartDto(string UserName, IReadOnlyList<ShoppingCartItemDto> Items, decimal TotalPrice)
{
    public static ShoppingCartDto From(ShoppingCart cart) => new(
        cart.UserName,
        cart.Items
            .Select(item => new ShoppingCartItemDto(item.ProductId, item.ProductName, item.Color, item.Quantity, item.Price))
            .ToList(),
        cart.TotalPrice);
}

/// <summary>A basket as a client sends it to be stored. Any total it carries is ignored.</summary>
public record StoreShoppingCartDto(string UserName, IReadOnlyList<ShoppingCartItemDto> Items);

public record ShoppingCartItemDto(Guid ProductId, string ProductName, string Color, int Quantity, decimal Price);
