using BuildingBlocks.Messaging.Events;

namespace Basket.API.Basket.CheckoutBasket;

public record CheckoutBasketCommand(BasketCheckoutDto BasketCheckoutDto)
    : ICommand<CheckoutBasketResult>;

public record CheckoutBasketResult(bool IsSuccess);

public class CheckoutBasketCommandValidator : AbstractValidator<CheckoutBasketCommand>
{
    public CheckoutBasketCommandValidator()
    {
        RuleFor(x => x.BasketCheckoutDto)
            .NotNull()
            .WithMessage("BasketCheckoutDto can't be null");

        RuleFor(x => x.BasketCheckoutDto.UserName)
            .NotEmpty()
            .WithMessage("UserName is required");
    }
}

/// <summary>
/// Records the checkout; it does not publish anything. Publishing used to happen
/// here, followed by deleting the basket - two separate writes, so a crash in
/// between sent the event and kept the basket, and the next checkout created a
/// second order. The basket deletion and the event now commit together in the
/// outbox, and the outbox relay publishes the event afterwards.
/// </summary>
public class CheckoutBasketCommandHandler(IBasketRepository repository)
    : ICommandHandler<CheckoutBasketCommand, CheckoutBasketResult>
{
    public async Task<CheckoutBasketResult> Handle(
        CheckoutBasketCommand command,
        CancellationToken cancellationToken)
    {
        var draft = command.BasketCheckoutDto.Adapt<BasketCheckoutEvent>();

        await repository.CheckoutBasket(draft, cancellationToken);

        return new CheckoutBasketResult(true);
    }
}
