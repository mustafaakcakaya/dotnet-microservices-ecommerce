namespace Shopping.Web.Pages;

public class CartModel(
    IBasketService basketService,
    ICatalogService catalogService,
    IShopperContext shopper,
    ILogger<CartModel> logger) : PageModel
{
    public ShoppingCartModel Cart { get; private set; } = new();

    /// <summary>Catalog entries for the basket lines, for their photos; a basket line has none.</summary>
    public IReadOnlyDictionary<Guid, ProductModel> Products { get; private set; } = new Dictionary<Guid, ProductModel>();

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        Cart = await basketService.LoadUserBasket(shopper.UserName, cancellationToken);

        if (Cart.Items.Count > 0)
        {
            try
            {
                var response = await catalogService.GetProducts(1, 50, cancellationToken);
                Products = response.Products.ToDictionary(product => product.Id);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                // Photos are decoration here; the cart still works without them.
                logger.LogWarning(exception, "Could not load product photos for the cart");
            }
        }
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
