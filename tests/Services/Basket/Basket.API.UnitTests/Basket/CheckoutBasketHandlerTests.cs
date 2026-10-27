using Basket.API.Basket.CheckoutBasket;
using Basket.API.Dtos;
using Basket.API.Models;
using Basket.API.UnitTests.Data;

namespace Basket.API.UnitTests.Basket;

public sealed class CheckoutBasketHandlerTests
{
    [Fact]
    public async Task Handle_PassesTheAddressAndTokenOn()
    {
        var repository = new FakeBasketRepository();
        repository.Seed(new ShoppingCart("mustafa")
        {
            Items = [new ShoppingCartItem { ProductId = Guid.NewGuid(), ProductName = "iPhone X", Quantity = 1, Price = 500 }]
        });

        await new CheckoutBasketCommandHandler(repository)
            .Handle(new CheckoutBasketCommand(ValidDto()), CancellationToken.None);

        var checkout = Assert.Single(repository.CheckedOut);
        Assert.Equal("mustafa", checkout.UserName);
        Assert.Equal("pt_0123", checkout.Payment.PaymentToken);
        Assert.Equal("4444", checkout.Payment.CardLast4);
        Assert.Equal("Bahcelievler No:4", checkout.ShippingAddress.AddressLine);
        Assert.Equal(checkout.ShippingAddress, checkout.BillingAddress);
    }

    [Fact]
    public void Validator_RejectsACheckoutWithoutAToken()
    {
        var dto = ValidDto();
        dto.PaymentToken = "";

        var result = new CheckoutBasketCommandValidator().Validate(new CheckoutBasketCommand(dto));

        Assert.Contains(result.Errors, error => error.PropertyName.EndsWith(nameof(BasketCheckoutDto.PaymentToken)));
    }

    [Fact]
    public void Validator_AcceptsAValidCheckout()
    {
        var result = new CheckoutBasketCommandValidator().Validate(new CheckoutBasketCommand(ValidDto()));

        Assert.True(result.IsValid);
    }

    private static BasketCheckoutDto ValidDto() => new()
    {
        UserName = "mustafa",
        CustomerId = Guid.NewGuid(),
        FirstName = "Mustafa",
        LastName = "Akcakaya",
        EmailAddress = "mustafa@example.com",
        AddressLine = "Bahcelievler No:4",
        Country = "Turkey",
        State = "Istanbul",
        ZipCode = "34180",
        PaymentToken = "pt_0123",
        CardLast4 = "4444",
        PaymentMethod = 1
    };
}
