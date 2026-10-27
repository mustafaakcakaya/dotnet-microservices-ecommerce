using System.Text.Json;
using Basket.API.Models;

namespace Basket.API.UnitTests.Models;

public sealed class ShoppingCartTests
{
    private static readonly Guid Phone = Guid.NewGuid();

    [Fact]
    public void AddItem_OfTheSameProductAndColour_AddsToTheQuantity()
    {
        var cart = new ShoppingCart("mustafa");

        cart.AddItem(Phone, "iPhone X", "Black", 1, 500);
        cart.AddItem(Phone, "iPhone X", "black", 2, 500);

        var line = Assert.Single(cart.Items);
        Assert.Equal(3, line.Quantity);
        Assert.Equal(1500, cart.TotalPrice);
    }

    [Fact]
    public void AddItem_InAnotherColour_IsASeparateLine()
    {
        var cart = new ShoppingCart("mustafa");

        cart.AddItem(Phone, "iPhone X", "Black", 1, 500);
        cart.AddItem(Phone, "iPhone X", "White", 1, 500);

        Assert.Equal(2, cart.Items.Count);
    }

    [Theory]
    [InlineData(0, 500)]
    [InlineData(-1, 500)]
    [InlineData(1, -1)]
    public void AddItem_RejectsAnInvalidLine(int quantity, decimal price)
    {
        var cart = new ShoppingCart("mustafa");

        Assert.Throws<ArgumentOutOfRangeException>(() => cart.AddItem(Phone, "iPhone X", "Black", quantity, price));
        Assert.Empty(cart.Items);
    }

    [Fact]
    public void ApplyDiscount_LowersOnlyThatProduct()
    {
        var cart = new ShoppingCart("mustafa");
        cart.AddItem(Phone, "iPhone X", "Black", 2, 500);
        cart.AddItem(Guid.NewGuid(), "Xiaomi Mi 9", "Black", 1, 470);

        cart.ApplyDiscount("iPhone X", 150);

        Assert.Equal(350, cart.Items.Single(item => item.ProductName == "iPhone X").Price);
        Assert.Equal(470, cart.Items.Single(item => item.ProductName == "Xiaomi Mi 9").Price);
        Assert.Equal(1170, cart.TotalPrice);
    }

    [Fact]
    public void ApplyDiscount_LargerThanThePrice_MakesTheLineFreeNotNegative()
    {
        var cart = new ShoppingCart("mustafa");
        cart.AddItem(Phone, "iPhone X", "Black", 1, 100);

        cart.ApplyDiscount("iPhone X", 150);

        Assert.Equal(0, cart.TotalPrice);
    }

    // The cache stores baskets with System.Text.Json; private setters must still round-trip.
    [Fact]
    public void Json_RoundTrip_KeepsTheLines()
    {
        var cart = new ShoppingCart("mustafa");
        cart.AddItem(Phone, "iPhone X", "Black", 2, 500);

        var copy = JsonSerializer.Deserialize<ShoppingCart>(JsonSerializer.Serialize(cart))!;

        Assert.Equal("mustafa", copy.UserName);
        var line = Assert.Single(copy.Items);
        Assert.Equal((Phone, "iPhone X", "Black", 2, 500m), (line.ProductId, line.ProductName, line.Color, line.Quantity, line.Price));
    }
}
