using Basket.API.Exceptions;
using Basket.API.Models;

namespace Basket.API.UnitTests.Models;

public sealed class ShoppingCartCheckoutTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 27, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ToCheckout_TakesLinesAndTotalFromTheBasket()
    {
        var cart = Cart("mustafa",
            Item("Huawei Plus", quantity: 2, price: 650),
            Item("Xiaomi Mi 9", quantity: 1, price: 470));

        var checkout = cart.ToCheckout(TestCheckouts.Draft("mustafa"), Guid.NewGuid(), Now);

        Assert.Equal(1770, checkout.TotalPrice);
        Assert.Collection(checkout.Items,
            line => Assert.Equal(("Huawei Plus", 2, 650m), (line.ProductName, line.Quantity, line.UnitPrice)),
            line => Assert.Equal(("Xiaomi Mi 9", 1, 470m), (line.ProductName, line.Quantity, line.UnitPrice)));
        Assert.Equal(Now.UtcDateTime, checkout.OccurredOnUtc);
    }

    // Customer, addresses and payment are what the basket does not know.
    [Fact]
    public void ToCheckout_KeepsWhatTheDraftContributes()
    {
        var draft = TestCheckouts.Draft("mustafa");

        var checkout = Cart("mustafa", Item("iPhone X", 1, 100)).ToCheckout(draft, Guid.NewGuid(), Now);

        Assert.Equal(draft.CustomerId, checkout.CustomerId);
        Assert.Equal(draft.ShippingAddress, checkout.ShippingAddress);
        Assert.Equal(draft.Payment, checkout.Payment);
    }

    [Fact]
    public void ToCheckout_OfTheSameVersion_GivesTheSameId()
    {
        var version = Guid.NewGuid();
        var cart = Cart("mustafa", Item("iPhone X", 1, 100));

        var first = cart.ToCheckout(TestCheckouts.Draft("mustafa"), version, Now);
        var second = cart.ToCheckout(TestCheckouts.Draft("mustafa"), version, Now.AddSeconds(1));

        Assert.Equal(first.Id, second.Id);
        Assert.NotEqual(first.Id, cart.ToCheckout(TestCheckouts.Draft("mustafa"), Guid.NewGuid(), Now).Id);
    }

    [Fact]
    public void ToCheckout_OfAnEmptyBasket_IsRejected() =>
        Assert.Throws<EmptyBasketException>(() =>
            Cart("mustafa").ToCheckout(TestCheckouts.Draft("mustafa"), Guid.NewGuid(), Now));

    [Fact]
    public void ToCheckout_ForAnotherUser_IsRejected() =>
        Assert.Throws<InvalidOperationException>(() =>
            Cart("mustafa", Item("iPhone X", 1, 100)).ToCheckout(TestCheckouts.Draft("someone-else"), Guid.NewGuid(), Now));

    private static ShoppingCart Cart(string userName, params (string Name, int Quantity, decimal Price)[] items)
    {
        var cart = new ShoppingCart(userName);
        foreach (var (name, quantity, price) in items)
        {
            cart.AddItem(Guid.NewGuid(), name, "Black", quantity, price);
        }

        return cart;
    }

    private static (string, int, decimal) Item(string name, int quantity, decimal price) => (name, quantity, price);
}
