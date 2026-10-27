using System.ComponentModel.DataAnnotations;

namespace Shopping.Web.Models.Basket;

/// <summary>
/// The checkout form. The card fields are posted here but leave this app only
/// towards the Payment service; Basket receives the token it returns instead
/// (see <see cref="BasketCheckoutDto"/>).
/// </summary>
public sealed class BasketCheckoutModel
{
    [Required]
    public string FirstName { get; set; } = default!;

    [Required]
    public string LastName { get; set; } = default!;

    [Required, EmailAddress]
    public string EmailAddress { get; set; } = default!;

    [Required]
    public string AddressLine { get; set; } = default!;

    [Required]
    public string Country { get; set; } = default!;

    [Required]
    public string State { get; set; } = default!;

    [Required]
    public string ZipCode { get; set; } = default!;

    [Required]
    public string CardName { get; set; } = default!;

    [Required, CreditCard]
    public string CardNumber { get; set; } = default!;

    [Required, RegularExpression("^(0[1-9]|1[0-2])/[0-9]{2}$", ErrorMessage = "Use MM/YY.")]
    public string Expiration { get; set; } = default!;

    [Required, RegularExpression("^[0-9]{3,4}$")]
    public string CVV { get; set; } = default!;

    [Range(1, 3)]
    public int PaymentMethod { get; set; } = 1;
}

/// <summary>What Basket receives at checkout: the address and a payment token, no card.</summary>
public sealed record BasketCheckoutDto(
    string UserName,
    Guid CustomerId,
    string FirstName,
    string LastName,
    string EmailAddress,
    string AddressLine,
    string Country,
    string State,
    string ZipCode,
    string PaymentToken,
    string CardLast4,
    int PaymentMethod);

public sealed record CheckoutBasketRequest(BasketCheckoutDto BasketCheckoutDto);
public sealed record CheckoutBasketResponse(bool IsSuccess);
