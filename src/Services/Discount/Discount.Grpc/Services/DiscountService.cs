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

    public override async Task<CouponModel> CreateDiscount(CreateDiscountRequest request, ServerCallContext context)
    {
        logger.LogInformation("CreateDiscount invoked for product {ProductName}", request.Coupon?.ProductName ?? "(null)");

        var coupon = request.Coupon.Adapt<Coupon>();

        if (coupon is null)
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Invalid request object."));
        }

        dbContext.Coupons.Add(coupon);
        await dbContext.SaveChangesAsync();

        logger.LogInformation(
            "Discount is successfully created. ProductName: {ProductName}",
            coupon.ProductName);

        return coupon.Adapt<CouponModel>();
    }

    public override async Task<CouponModel> UpdateDiscount(UpdateDiscountRequest request, ServerCallContext context)
    {
        logger.LogInformation("UpdateDiscount invoked for product {ProductName}", request.Coupon?.ProductName ?? "(null)");

        var coupon = request.Coupon.Adapt<Coupon>();

        if (coupon is null)
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Invalid request object."));
        }

        dbContext.Coupons.Update(coupon);
        await dbContext.SaveChangesAsync();

        logger.LogInformation(
            "Discount is successfully updated. ProductName: {ProductName}",
            coupon.ProductName);

        return coupon.Adapt<CouponModel>();
    }

    public override async Task<DeleteDiscountResponse> DeleteDiscount(DeleteDiscountRequest request, ServerCallContext context)
    {
        logger.LogInformation("DeleteDiscount invoked for product {ProductName}", request.ProductName);

        var coupon = await dbContext.Coupons.FirstOrDefaultAsync(
            x => x.ProductName == request.ProductName);

        if (coupon is null)
        {
            throw new RpcException(new Status(
                StatusCode.NotFound,
                $"Discount with ProductName = {request.ProductName} is not found."));
        }

        dbContext.Coupons.Remove(coupon);
        await dbContext.SaveChangesAsync();

        logger.LogInformation(
            "Discount is successfully deleted. ProductName: {ProductName}",
            request.ProductName);

        return new DeleteDiscountResponse { Success = "true" };
    }
}
