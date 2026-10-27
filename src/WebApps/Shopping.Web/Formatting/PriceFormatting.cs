using System.Globalization;

namespace Shopping.Web.Formatting;

public static class PriceFormatting
{
    private static readonly CultureInfo Turkish = CultureInfo.GetCultureInfo("tr-TR");

    /// <summary>
    /// "3.299 TL" for whole amounts and "2.639,20 TL" otherwise, the way Turkish
    /// storefronts print prices.
    /// </summary>
    public static string ToTl(this decimal amount) =>
        (decimal.Truncate(amount) == amount
            ? amount.ToString("N0", Turkish)
            : amount.ToString("N2", Turkish)) + " TL";
}
