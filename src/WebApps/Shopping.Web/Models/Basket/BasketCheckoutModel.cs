using System.ComponentModel.DataAnnotations;

namespace Shopping.Web.Models.Basket;

/// <summary>
/// The checkout form. The card fields are posted here but leave this app only
/// towards the Payment service; Basket receives the token it returns instead
/// (see <see cref="BasketCheckoutDto"/>).
/// </summary>
public sealed class BasketCheckoutModel
{
    [Required(ErrorMessage = "Ad zorunludur.")]
    public string FirstName { get; set; } = default!;

    [Required(ErrorMessage = "Soyad zorunludur.")]
    public string LastName { get; set; } = default!;

    [Required(ErrorMessage = "E-posta zorunludur."), EmailAddress(ErrorMessage = "Geçerli bir e-posta girin.")]
    public string EmailAddress { get; set; } = default!;

    [Required(ErrorMessage = "Adres zorunludur.")]
    public string AddressLine { get; set; } = default!;

    [Required(ErrorMessage = "Ülke zorunludur.")]
    public string Country { get; set; } = default!;

    [Required(ErrorMessage = "İl zorunludur.")]
    public string State { get; set; } = default!;

    [Required(ErrorMessage = "Posta kodu zorunludur.")]
    public string ZipCode { get; set; } = default!;

    [Required(ErrorMessage = "Kart üzerindeki isim zorunludur.")]
    public string CardName { get; set; } = default!;

    [Required(ErrorMessage = "Kart numarası zorunludur."), CreditCard(ErrorMessage = "Geçerli bir kart numarası girin.")]
    public string CardNumber { get; set; } = default!;

    [Required(ErrorMessage = "Son kullanma tarihi zorunludur."), RegularExpression("^(0[1-9]|1[0-2])/[0-9]{2}$", ErrorMessage = "AA/YY biçiminde girin.")]
    public string Expiration { get; set; } = default!;

    [Required(ErrorMessage = "CVV zorunludur."), RegularExpression("^[0-9]{3,4}$", ErrorMessage = "CVV 3 veya 4 haneli olmalı.")]
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
