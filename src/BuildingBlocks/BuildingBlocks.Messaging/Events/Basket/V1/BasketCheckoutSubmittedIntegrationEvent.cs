namespace BuildingBlocks.Messaging.Events.Basket.V1;

/// <summary>
/// Published when a shopper checks a basket out; Ordering turns it into an order.
/// The lines are the basket exactly as it was checked out, so the order is built
/// from what the shopper bought rather than from anything Ordering assumes.
/// No card data travels here: the card stays in the Payment service and the
/// event carries only the token that refers to it.
/// </summary>
[IntegrationEventContract("basket.checkout-submitted", schemaVersion: 1)]
public sealed record BasketCheckoutSubmittedIntegrationEvent : IntegrationEvent
{
    public string UserName { get; init; } = default!;
    public Guid CustomerId { get; init; }

    /// <summary>Sum of the lines; consumers check it against the lines and reject a mismatch.</summary>
    public decimal TotalPrice { get; init; }

    public IReadOnlyList<BasketCheckoutLine> Items { get; init; } = [];

    public CheckoutAddress ShippingAddress { get; init; } = default!;
    public CheckoutAddress BillingAddress { get; init; } = default!;

    public CheckoutPayment Payment { get; init; } = default!;
}

public sealed record BasketCheckoutLine(Guid ProductId, string ProductName, int Quantity, decimal UnitPrice);

public sealed record CheckoutAddress(
    string FirstName,
    string LastName,
    string EmailAddress,
    string AddressLine,
    string Country,
    string State,
    string ZipCode);

/// <param name="PaymentToken">Opaque reference issued by the Payment service.</param>
/// <param name="CardLast4">Last four digits, for display only.</param>
public sealed record CheckoutPayment(string PaymentToken, string CardLast4, int PaymentMethod);
