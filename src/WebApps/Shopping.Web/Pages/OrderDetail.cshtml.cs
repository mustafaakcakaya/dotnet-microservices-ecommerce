using System.Net;

namespace Shopping.Web.Pages;

public class OrderDetailModel(
    IOrderingService orderingService,
    ICatalogService catalogService,
    IShopperContext shopper) : PageModel
{
    public OrderModel Order { get; private set; } = default!;

    /// <summary>Product names by id; an order only stores the product id.</summary>
    public IReadOnlyDictionary<Guid, string> ProductNames { get; private set; } = new Dictionary<Guid, string>();

    public decimal Total => Order.OrderItems.Sum(item => item.Price * item.Quantity);

    public async Task<IActionResult> OnGetAsync(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            Order = (await orderingService.GetOrderById(id, cancellationToken)).Order;
        }
        catch (ApiException exception) when (exception.StatusCode == HttpStatusCode.NotFound)
        {
            return NotFound();
        }

        // Another shopper's order is reported as missing rather than forbidden,
        // so the page does not confirm that the id exists.
        if (Order.CustomerId != shopper.CustomerId)
        {
            return NotFound();
        }

        ProductNames = await LoadProductNamesAsync(cancellationToken);
        return Page();
    }

    public string ProductName(Guid productId) =>
        ProductNames.TryGetValue(productId, out var name) ? name : "Product no longer in the catalog";

    private async Task<Dictionary<Guid, string>> LoadProductNamesAsync(CancellationToken cancellationToken)
    {
        var names = new Dictionary<Guid, string>();
        foreach (var productId in Order.OrderItems.Select(item => item.ProductId).Distinct())
        {
            try
            {
                var product = (await catalogService.GetProduct(productId, cancellationToken)).Product;
                names[productId] = product.Name;
            }
            catch (ApiException exception) when (exception.StatusCode == HttpStatusCode.NotFound)
            {
                // Removed from the catalog since the order was placed; shown as such.
            }
        }

        return names;
    }
}
