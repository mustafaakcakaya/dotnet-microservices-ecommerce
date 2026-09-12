namespace Shopping.Web.Pages;

public class IndexModel(
    ICatalogService catalogService,
    IBasketService basketService,
    IShopperContext shopper,
    ILogger<IndexModel> logger) : PageModel
{
    public IReadOnlyList<ProductModel> Products { get; private set; } = [];

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("Loading storefront home page");
        var response = await catalogService.GetProducts(1, 12, cancellationToken);
        Products = response.Products.ToList();
    }

    public async Task<IActionResult> OnPostAddToCartAsync(
        Guid productId,
        CancellationToken cancellationToken)
    {
        var productResponse = await catalogService.GetProduct(productId, cancellationToken);
        var basket = await basketService.LoadUserBasket(shopper.UserName, cancellationToken);

        basket.AddItem(productResponse.Product);
        await basketService.StoreBasket(new StoreBasketRequest(basket), cancellationToken);

        return RedirectToPage("Cart");
    }
}
