using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace Shopping.Web.Models.Basket;

public sealed class BasketCheckoutModel
{
    [ValidateNever]
    public string UserName { get; set; } = default!;
    public Guid CustomerId { get; set; }
    public decimal TotalPrice { get; set; }

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

    [Required]
    public string Expiration { get; set; } = default!;

    [Required, RegularExpression("^[0-9]{3,4}$")]
    public string CVV { get; set; } = default!;

    [Range(1, 3)]
    public int PaymentMethod { get; set; } = 1;
}

public sealed record CheckoutBasketRequest(BasketCheckoutModel BasketCheckoutDto);
public sealed record CheckoutBasketResponse(bool IsSuccess);
