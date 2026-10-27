using Microsoft.AspNetCore.Mvc;

namespace Shopping.Web.ViewComponents;

/// <summary>
/// The cart button in the header, with the number of items in the shopper's basket.
/// </summary>
public class CartSummaryViewComponent(
    IBasketService basketService,
    IShopperContext shopper,
    ILogger<CartSummaryViewComponent> logger) : ViewComponent
{
    public async Task<IViewComponentResult> InvokeAsync()
    {
        int? itemCount;
        try
        {
            var cart = await basketService.LoadUserBasket(shopper.UserName, HttpContext.RequestAborted);
            itemCount = cart.Items.Sum(item => item.Quantity);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // The header is on every page; a basket outage must not take the page down
            // with it. The button is shown without a count instead.
            logger.LogWarning(exception, "Could not load the basket for the cart summary");
            itemCount = null;
        }

        return View(itemCount);
    }
}
