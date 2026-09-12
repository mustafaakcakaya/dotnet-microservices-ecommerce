namespace Shopping.Web.Pages;

public class ProductDetailModel(
    ICatalogService catalogService,
    IBasketService basketService,
    IShopperContext shopper) : PageModel
{
    public ProductModel Product { get; private set; } = default!;

    [BindProperty]
    public string Color { get; set; } = "Black";

    [BindProperty]
    public int Quantity { get; set; } = 1;

    public async Task OnGetAsync(Guid productId, CancellationToken cancellationToken)
    {
        Product = (await catalogService.GetProduct(productId, cancellationToken)).Product;
    }

    public async Task<IActionResult> OnPostAddToCartAsync(
        Guid productId,
        CancellationToken cancellationToken)
    {
        Quantity = Math.Clamp(Quantity, 1, 100);

        var product = (await catalogService.GetProduct(productId, cancellationToken)).Product;
        var basket = await basketService.LoadUserBasket(shopper.UserName, cancellationToken);

        basket.AddItem(product, Quantity, Color);
        await basketService.StoreBasket(new StoreBasketRequest(basket), cancellationToken);

        return RedirectToPage("Cart");
    }
}
