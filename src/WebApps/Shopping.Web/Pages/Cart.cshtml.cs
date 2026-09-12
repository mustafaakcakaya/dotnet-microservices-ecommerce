namespace Shopping.Web.Pages;

public class CartModel(
    IBasketService basketService,
    IShopperContext shopper) : PageModel
{
    public ShoppingCartModel Cart { get; private set; } = new();

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        Cart = await basketService.LoadUserBasket(shopper.UserName, cancellationToken);
    }

    public async Task<IActionResult> OnPostRemoveFromCartAsync(
        Guid productId,
        CancellationToken cancellationToken)
    {
        var cart = await basketService.LoadUserBasket(shopper.UserName, cancellationToken);
        cart.RemoveItem(productId);

        if (cart.Items.Count == 0)
        {
            await basketService.DeleteBasket(shopper.UserName, cancellationToken);
        }
        else
        {
            await basketService.StoreBasket(new StoreBasketRequest(cart), cancellationToken);
        }

        return RedirectToPage();
    }
}
