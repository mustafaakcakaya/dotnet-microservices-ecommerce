namespace Shopping.Web.Pages;

public class OrderListModel(
    IOrderingService orderingService,
    IShopperContext shopper) : PageModel
{
    public IReadOnlyList<OrderModel> Orders { get; private set; } = [];

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        var response = await orderingService.GetOrdersByCustomer(
            shopper.CustomerId,
            cancellationToken);

        Orders = response.Orders.ToList();
    }
}
