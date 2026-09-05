using Discount.Grpc.Models;
using Microsoft.EntityFrameworkCore;

namespace Discount.Grpc.Data;

public static class DiscountInitialData
{
    /// <summary>
    /// Inserts the sample coupons when the table is empty, so re-running is a
    /// no-op.
    /// </summary>
    public static async Task SeedAsync(DiscountContext context, CancellationToken cancellationToken = default)
    {
        if (await context.Coupons.AnyAsync(cancellationToken))
        {
            return;
        }

        context.Coupons.AddRange(GetPreconfiguredCoupons());
        await context.SaveChangesAsync(cancellationToken);
    }

    // Product names must match the Catalog service exactly: discounts are looked
    // up by ProductName, so "Iphone X" (the original spelling here) never matched
    // the "iPhone X" the catalog seeds and that coupon never applied. Keep these
    // in step with CatalogInitialData when either side changes.
    private static IEnumerable<Coupon> GetPreconfiguredCoupons() =>
    [
        new Coupon { ProductName = "iPhone X", Description = "Iphone discount", Amount = 150 },
        new Coupon { ProductName = "Samsung 10", Description = "Samsung discount", Amount = 100 }
    ];
}
