using BuildingBlocks.Messaging.Events.Basket.V1;

namespace Basket.API.IntegrationTests;

/// <summary>A checkout as the handler hands it to the repository: no lines, no total yet.</summary>
internal static class TestCheckouts
{
    public static readonly CheckoutAddress Address = new(
        "Mustafa", "Akcakaya", "mustafa@example.com", "Bahcelievler No:4", "Turkey", "Istanbul", "34180");

    public static BasketCheckoutSubmittedIntegrationEvent Draft(string userName) => new()
    {
        UserName = userName,
        CustomerId = Guid.NewGuid(),
        ShippingAddress = Address,
        BillingAddress = Address,
        Payment = new CheckoutPayment("pt_test", "4444", 1)
    };
}
