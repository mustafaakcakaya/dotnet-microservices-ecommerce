namespace Shopping.Web.Pages;

public class ProductListModel(
    ICatalogService catalogService,
    IBasketService basketService,
    IShopperContext shopper) : PageModel
{
    public IReadOnlyList<string> Categories { get; private set; } = [];
    public IReadOnlyList<ProductModel> Products { get; private set; } = [];

    [BindProperty(SupportsGet = true)]
    public string? Category { get; set; }

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        var response = await catalogService.GetProducts(1, 50, cancellationToken);
        var products = response.Products.ToList();

        Categories = products
            .SelectMany(product => product.Category)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(category => category)
            .ToList();

        Products = string.IsNullOrWhiteSpace(Category)
            ? products
            : products.Where(product =>
                product.Category.Contains(Category, StringComparer.OrdinalIgnoreCase)).ToList();
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
