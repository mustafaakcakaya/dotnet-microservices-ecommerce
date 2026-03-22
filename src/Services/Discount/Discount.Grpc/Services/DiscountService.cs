using Discount.Grpc.Data;
using Grpc.Core;
using Microsoft.EntityFrameworkCore;

namespace Discount.Grpc.Services;

public class DiscountService(
    ILogger<DiscountService> logger,
    IDbContextFactory<DiscountContext> dbFactory) : DiscountProtoService.DiscountProtoServiceBase
{
    public override async Task<CouponModel> GetDiscount(GetDiscountRequest request, ServerCallContext context)
    {
        logger.LogInformation("GetDiscount invoked for product {ProductName}", request.ProductName);

        await using var db = await dbFactory.CreateDbContextAsync();
        var coupon = await db.Coupons
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.ProductName == request.ProductName);

        if (coupon is null)
        {
            throw new RpcException(new Status(StatusCode.NotFound,
                $"No discount found for product '{request.ProductName}'."));
        }

        return new CouponModel
        {
            Id = coupon.Id,
            ProductName = coupon.ProductName,
            Desciption = coupon.Description,
            Amount = coupon.Amount
        };
    }

    public override Task<CouponModel> CreateDiscount(CreateDiscountRequest request, ServerCallContext context)
    {
        logger.LogInformation("CreateDiscount invoked for product {ProductName}", request.Coupon?.ProductName ?? "(null)");
        ArgumentNullException.ThrowIfNull(request.Coupon);

        return Task.FromResult(request.Coupon);
    }

    public override Task<CouponModel> UpdateDiscount(UpdateDiscountRequest request, ServerCallContext context)
    {
        logger.LogInformation("UpdateDiscount invoked for product {ProductName}", request.Coupon?.ProductName ?? "(null)");
        ArgumentNullException.ThrowIfNull(request.Coupon);

        return Task.FromResult(request.Coupon);
    }

    public override Task<DeleteDiscountResponse> DeleteDiscount(DeleteDiscountRequest request, ServerCallContext context)
    {
        logger.LogInformation("DeleteDiscount invoked for product {ProductName}", request.ProductName);

        return Task.FromResult(new DeleteDiscountResponse { Success = "true" });
    }
}