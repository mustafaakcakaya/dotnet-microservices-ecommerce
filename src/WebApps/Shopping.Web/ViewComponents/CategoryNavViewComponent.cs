using Microsoft.AspNetCore.Mvc;

namespace Shopping.Web.ViewComponents;

/// <summary>
/// The category bar under the header, built from the categories the catalog
/// actually has rather than a fixed list.
/// </summary>
public class CategoryNavViewComponent(
    ICatalogService catalogService,
    ILogger<CategoryNavViewComponent> logger) : ViewComponent
{
    public async Task<IViewComponentResult> InvokeAsync()
    {
        IReadOnlyList<string> categories;
        try
        {
            var response = await catalogService.GetProducts(1, 50, HttpContext.RequestAborted);
            categories = response.Products
                .SelectMany(product => product.Category)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(category => category)
                .ToList();
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // Like the cart summary: a catalog outage leaves the bar empty, not the page broken.
            logger.LogWarning(exception, "Could not load categories for the navigation bar");
            categories = [];
        }

        return View(categories);
    }
}
