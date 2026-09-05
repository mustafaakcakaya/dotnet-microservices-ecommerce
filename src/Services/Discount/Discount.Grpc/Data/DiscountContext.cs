using Discount.Grpc.Models;
using Microsoft.EntityFrameworkCore;

namespace Discount.Grpc.Data;

public class DiscountContext : DbContext
{
    public DbSet<Coupon> Coupons { get; set; } =  default!;

    public DiscountContext(DbContextOptions<DiscountContext> options)
        : base(options)
    {
    }

    // Sample coupons used to live here as HasData, which welds the seed to the
    // schema: it ships with every migration and cannot be turned off per
    // environment. They now live in DiscountInitialData, so seeding is an
    // explicit, switchable step like it already is in Catalog and Ordering.
}
