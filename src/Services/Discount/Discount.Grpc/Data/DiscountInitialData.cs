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

    // Product names must match CatalogInitialData exactly: discounts are looked up
    // by an exact ProductName match, so a coupon whose name differs simply never
    // applies. Keep the two in step whenever either side changes.
    private static IEnumerable<Coupon> GetPreconfiguredCoupons() =>
    [
        new Coupon { ProductName = "iPhone X", Description = "iPhone discount", Amount = 150 },
        new Coupon { ProductName = "Samsung 10", Description = "Samsung discount", Amount = 100 }
    ];
}
