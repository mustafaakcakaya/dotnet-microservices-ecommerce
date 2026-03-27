namespace Discount.Grpc.Services;

public class DiscountService(
    ILogger<DiscountService> logger,
    DiscountContext dbContext) : DiscountProtoService.DiscountProtoServiceBase
{
    public override async Task<CouponModel> GetDiscount(GetDiscountRequest request, ServerCallContext context)
    {
        logger.LogInformation("GetDiscount invoked for product {ProductName}", request.ProductName);

        var coupon = await dbContext.Coupons.FirstOrDefaultAsync(
            x => x.ProductName == request.ProductName);
        
        if (coupon is null)
        {
            coupon = new Coupon
            {
                ProductName = "No Discount",
                Amount = 0,
                Description = "No Discount Desc"
            };
        }
        
        logger.LogInformation("Discount is retrieved for ProductName :  {ProductName}, Amount : {Amount}, ", request.ProductName, coupon.Amount);
        var couponModel = coupon.Adapt<CouponModel>();
        return couponModel;
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