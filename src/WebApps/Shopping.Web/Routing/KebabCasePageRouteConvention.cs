using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.ApplicationModels;

namespace Shopping.Web.Routing;

/// <summary>
/// Serves pages at lowercase, hyphenated paths: OrderList at /order-list,
/// ProductDetail at /product-detail. Page names stay PascalCase in code, and
/// links built with asp-page follow automatically.
/// </summary>
public sealed partial class KebabCasePageRouteConvention : IPageRouteModelConvention
{
    public void Apply(PageRouteModel model)
    {
        foreach (var selector in model.Selectors)
        {
            var template = selector.AttributeRouteModel?.Template;
            if (!string.IsNullOrEmpty(template))
            {
                selector.AttributeRouteModel!.Template = ToKebabCase(template);
            }
        }
    }

    internal static string ToKebabCase(string template) =>
        WordBoundary().Replace(template, "$1-$2").ToLowerInvariant();

    // A lowercase letter or digit followed by an uppercase one starts a new word.
    [GeneratedRegex("([a-z0-9])([A-Z])")]
    private static partial Regex WordBoundary();
}
