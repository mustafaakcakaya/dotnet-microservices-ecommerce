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

public class StoreBasketCommandHandler 
        : ICommandHandler<StoreBasketCommand, StoreBasketResult>
{
    public async Task<StoreBasketResult> Handle(StoreBasketCommand command, CancellationToken cancellationToken)
    {
        if (command.Cart is null)
        {
            // Let validation middleware / exception handler generate a proper 400 response
            throw new ValidationException("Cart can not be empty");
        }

        ShoppingCart cart = command.Cart;
        
        //TODO: Implement the logic to store the basket later.
        //await _basketRepository.StoreBasket(cart);
        //TODO: update cache later.

        return new StoreBasketResult(cart.UserName);
    }
}