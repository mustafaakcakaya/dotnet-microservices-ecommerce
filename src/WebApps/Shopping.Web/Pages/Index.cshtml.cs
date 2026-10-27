namespace Shopping.Web.Pages;

public class IndexModel(
    ICatalogService catalogService,
    IBasketService basketService,
    IShopperContext shopper,
    ILogger<IndexModel> logger) : PageModel
{
    public IReadOnlyList<ProductModel> Products { get; private set; } = [];

    /// <summary>One tile per catalog category, shown with its first product.</summary>
    public IReadOnlyList<CategoryTile> Categories { get; private set; } = [];

    public sealed record CategoryTile(string Name, int ProductCount, ProductModel Cover);

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("Loading storefront home page");
        var response = await catalogService.GetProducts(1, 12, cancellationToken);
        Products = response.Products.ToList();

        Categories = Products
            .SelectMany(product => product.Category.Select(category => (category, product)))
            .GroupBy(pair => pair.category, StringComparer.OrdinalIgnoreCase)
            .OrderBy(group => group.Key)
            .Select(group => new CategoryTile(group.Key, group.Count(), group.First().product))
            .Take(3)
            .ToList();
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
