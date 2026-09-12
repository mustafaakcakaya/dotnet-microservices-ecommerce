namespace Shopping.Web.Pages;

public class CheckoutModel(
    IBasketService basketService,
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

        Order.CustomerId = shopper.CustomerId;
        Order.UserName = shopper.UserName;
        Order.TotalPrice = Cart.TotalPrice;

        logger.LogInformation("Submitting checkout for shopper {UserName}", shopper.UserName);
        var response = await basketService.CheckoutBasket(
            new CheckoutBasketRequest(Order),
            cancellationToken);

        if (!response.IsSuccess)
        {
            ModelState.AddModelError(string.Empty, "Checkout could not be completed.");
            return Page();
        }

        return RedirectToPage("Confirmation", "OrderSubmitted");
    }
}
