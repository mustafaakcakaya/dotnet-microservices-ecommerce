using System.Net;

namespace Shopping.Web.Pages;

public class CheckoutModel(
    IBasketService basketService,
    IPaymentService paymentService,
    IShopperContext shopper,
    ILogger<CheckoutModel> logger) : PageModel
{
    [BindProperty]
    public BasketCheckoutModel Order { get; set; } = new();

    public ShoppingCartModel Cart { get; private set; } = new();

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        Cart = await basketService.LoadUserBasket(shopper.UserName, cancellationToken);
        return Cart.Items.Count == 0 ? RedirectToPage("Cart") : Page();
    }

    public async Task<IActionResult> OnPostCheckoutAsync(CancellationToken cancellationToken)
    {
        Cart = await basketService.LoadUserBasket(shopper.UserName, cancellationToken);

        if (Cart.Items.Count == 0)
        {
            ModelState.AddModelError(string.Empty, "Your basket is empty.");
        }

        if (!ModelState.IsValid)
        {
            return Page();
        }

        // The card goes to the Payment service only; Basket gets the token.
        TokenizeCardResponse card;
        try
        {
            card = await paymentService.TokenizeCard(
                new TokenizeCardRequest(
                    shopper.CustomerId,
                    Order.CardName,
                    Order.CardNumber,
                    Order.Expiration,
                    Order.CVV),
                cancellationToken);
        }
        catch (ApiException exception) when (exception.StatusCode == HttpStatusCode.BadRequest)
        {
            ModelState.AddModelError(string.Empty, "The card details were not accepted. Please check them.");
            return Page();
        }

        var checkout = new BasketCheckoutDto(
            shopper.UserName,
            shopper.CustomerId,
            Order.FirstName,
            Order.LastName,
            Order.EmailAddress,
            Order.AddressLine,
            Order.Country,
            Order.State,
            Order.ZipCode,
            card.PaymentToken,
            card.CardLast4,
            Order.PaymentMethod);

        logger.LogInformation("Submitting checkout for shopper {UserName}", shopper.UserName);
        var response = await basketService.CheckoutBasket(
            new CheckoutBasketRequest(checkout),
            cancellationToken);

        if (!response.IsSuccess)
        {
            ModelState.AddModelError(string.Empty, "Checkout could not be completed.");
            return Page();
        }

        return RedirectToPage("Confirmation", "OrderSubmitted");
    }
}
