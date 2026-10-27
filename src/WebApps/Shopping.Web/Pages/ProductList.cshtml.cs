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

    [BindProperty(SupportsGet = true)]
    public string? Sort { get; set; }

    /// <summary>Sort options by query value; the catalog's own order is the default.</summary>
    public static readonly IReadOnlyList<(string Value, string Label)> SortOptions =
    [
        ("", "Önerilen"),
        ("price-asc", "En Düşük Fiyat"),
        ("price-desc", "En Yüksek Fiyat"),
        ("name", "Ürün Adı (A-Z)")
    ];

    public string SortLabel =>
        SortOptions.FirstOrDefault(option => option.Value == (Sort ?? "")).Label ?? "Önerilen";

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        var response = await catalogService.GetProducts(1, 50, cancellationToken);
        var products = response.Products.ToList();

        Categories = products
            .SelectMany(product => product.Category)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(category => category)
            .ToList();

        var filtered = string.IsNullOrWhiteSpace(Category)
            ? products
            : products.Where(product =>
                product.Category.Contains(Category, StringComparer.OrdinalIgnoreCase)).ToList();

        Products = Sort switch
        {
            "price-asc" => filtered.OrderBy(product => product.Price).ToList(),
            "price-desc" => filtered.OrderByDescending(product => product.Price).ToList(),
            "name" => filtered.OrderBy(product => product.Name, StringComparer.CurrentCultureIgnoreCase).ToList(),
            _ => filtered
        };
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
