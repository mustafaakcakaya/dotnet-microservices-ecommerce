namespace Basket.API.Basket.StoreBasket;

public record StoreBasketCommand(StoreShoppingCartDto Cart) : ICommand<StoreBasketResult>;

public record StoreBasketResult(string UserName);

public class StoreBasketCommandValidator : AbstractValidator<StoreBasketCommand>
{
    public StoreBasketCommandValidator()
    {
        RuleFor(x => x.Cart).NotNull().WithMessage("Cart can not be empty");
        RuleFor(x => x.Cart!.UserName)
            .NotEmpty().WithMessage("User name is required")
            .When(x => x.Cart is not null);

        // The same rules the basket enforces, checked here first so the client gets
        // a 400 with a reason instead of the basket refusing the line.
        RuleForEach(x => x.Cart!.Items)
            .ChildRules(item =>
            {
                item.RuleFor(x => x.ProductId).NotEmpty().WithMessage("ProductId is required");
                item.RuleFor(x => x.ProductName).NotEmpty().WithMessage("ProductName is required");
                item.RuleFor(x => x.Color).NotEmpty().WithMessage("Color is required");
                item.RuleFor(x => x.Quantity).GreaterThan(0).WithMessage("Quantity must be at least 1");
                item.RuleFor(x => x.Price).GreaterThanOrEqualTo(0).WithMessage("Price can not be negative");
            })
            .When(x => x.Cart?.Items is not null);
    }
}

public class StoreBasketCommandHandler(
    IBasketRepository basketRepository,
    DiscountProtoService.DiscountProtoServiceClient discountProtoService)
        : ICommandHandler<StoreBasketCommand, StoreBasketResult>
{
    public async Task<StoreBasketResult> Handle(StoreBasketCommand command, CancellationToken cancellationToken)
    {
        var cart = new ShoppingCart(command.Cart.UserName);
        foreach (var item in command.Cart.Items ?? [])
        {
            cart.AddItem(item.ProductId, item.ProductName, item.Color, item.Quantity, item.Price);
        }

        await DeductDiscounts(cart, cancellationToken);

        var basket = await basketRepository.StoreBasket(cart, cancellationToken);

        return new StoreBasketResult(basket.UserName);
    }

    private async Task DeductDiscounts(ShoppingCart cart, CancellationToken cancellationToken)
    {
        foreach (var productName in cart.Items.Select(item => item.ProductName).Distinct().ToList())
        {
            var coupon = await discountProtoService.GetDiscountAsync(
                new GetDiscountRequest { ProductName = productName },
                cancellationToken: cancellationToken);

            cart.ApplyDiscount(productName, (decimal)coupon.Amount);
        }
    }
}
