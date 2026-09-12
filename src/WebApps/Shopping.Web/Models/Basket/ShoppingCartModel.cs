namespace Shopping.Web.Models.Basket;

public sealed class ShoppingCartModel
{
    public string UserName { get; set; } = default!;
    public List<ShoppingCartItemModel> Items { get; set; } = [];
    public decimal TotalPrice => Items.Sum(item => item.Price * item.Quantity);

    public void AddItem(ProductModel product, int quantity = 1, string color = "Black")
    {
        var existingItem = Items.FirstOrDefault(item =>
            item.ProductId == product.Id &&
            string.Equals(item.Color, color, StringComparison.OrdinalIgnoreCase));

        if (existingItem is not null)
        {
            existingItem.Quantity += quantity;
            return;
        }

        Items.Add(new ShoppingCartItemModel
        {
            ProductId = product.Id,
            ProductName = product.Name,
            Price = product.Price,
            Quantity = quantity,
            Color = color
        });
    }

    public void RemoveItem(Guid productId) =>
        Items.RemoveAll(item => item.ProductId == productId);
}

public sealed class ShoppingCartItemModel
{
    public int Quantity { get; set; }
    public string Color { get; set; } = default!;
    public decimal Price { get; set; }
    public Guid ProductId { get; set; }
    public string ProductName { get; set; } = default!;
}

public sealed record GetBasketResponse(ShoppingCartModel Cart);
public sealed record StoreBasketRequest(ShoppingCartModel Cart);
public sealed record StoreBasketResponse(string UserName);
public sealed record DeleteBasketResponse(bool IsSuccess);
