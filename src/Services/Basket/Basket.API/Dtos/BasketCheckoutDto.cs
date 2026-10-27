namespace Basket.API.Dtos;

/// <summary>
/// What the shopper submits at checkout. The basket's lines and total are not
/// part of it - they are read from the stored basket. Card details are not part
/// of it either: the card goes to the Payment service, and only the token it
/// issues comes here.
/// </summary>
public class BasketCheckoutDto
{
    public string UserName { get; set; } = default!;
    public Guid CustomerId { get; set; }

    // Shipping and billing address
    public string FirstName { get; set; } = default!;
    public string LastName { get; set; } = default!;
    public string EmailAddress { get; set; } = default!;
    public string AddressLine { get; set; } = default!;
    public string Country { get; set; } = default!;
    public string State { get; set; } = default!;
    public string ZipCode { get; set; } = default!;

    // Payment
    public string PaymentToken { get; set; } = default!;
    public string CardLast4 { get; set; } = default!;
    public int PaymentMethod { get; set; }
}
