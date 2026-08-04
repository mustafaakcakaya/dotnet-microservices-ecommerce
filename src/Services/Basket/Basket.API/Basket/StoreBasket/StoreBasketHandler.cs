namespace Basket.API.Basket.StoreBasket;

public record StoreBasketCommand(ShoppingCart Cart) : ICommand<StoreBasketResult>;

public record StoreBasketResult(string UserName);

public class StoreBasketCommandValidator : AbstractValidator<StoreBasketCommand>
{
    public StoreBasketCommandValidator()
    {
        RuleFor(x => x.Cart).NotNull().WithMessage("Cart can not be empty");
        RuleFor(x => x.Cart!.UserName)
            .NotEmpty().WithMessage("User name is required")
            .When(x => x.Cart is not null);
    }
}

public class StoreBasketCommandHandler(
    IBasketRepository basketRepository,
    DiscountProtoService.DiscountProtoServiceClient discountProtoService)
        : ICommandHandler<StoreBasketCommand, StoreBasketResult>
{
    public async Task<StoreBasketResult> Handle(StoreBasketCommand command, CancellationToken cancellationToken)
    {
        await DeductDiscount(command.Cart, cancellationToken);

        var basket = await basketRepository.StoreBasket(command.Cart, cancellationToken);

        return new StoreBasketResult(basket.UserName);
    }

    private async Task DeductDiscount(ShoppingCart cart, CancellationToken cancellationToken)
    {
        foreach (var item in cart.Items)
        {
            var coupon = await discountProtoService.GetDiscountAsync(
                new GetDiscountRequest { ProductName = item.ProductName },
                cancellationToken: cancellationToken);

            item.Price -= coupon.Amount;
        }
    }
}
