using BuildingBlocks.Messaging.Events.Basket.V1;

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

        RuleFor(x => x.BasketCheckoutDto.PaymentToken)
            .NotEmpty()
            .WithMessage("PaymentToken is required; tokenize the card with the Payment service first");

        RuleFor(x => x.BasketCheckoutDto.CardLast4)
            .Matches("^[0-9]{4}$")
            .WithMessage("CardLast4 must be four digits");
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
        var dto = command.BasketCheckoutDto;
        var address = new CheckoutAddress(
            dto.FirstName,
            dto.LastName,
            dto.EmailAddress,
            dto.AddressLine,
            dto.Country,
            dto.State,
            dto.ZipCode);

        // Lines, total and id are filled in by the repository from the stored basket.
        var draft = new BasketCheckoutSubmittedIntegrationEvent
        {
            UserName = dto.UserName,
            CustomerId = dto.CustomerId,
            ShippingAddress = address,
            BillingAddress = address,
            Payment = new CheckoutPayment(dto.PaymentToken, dto.CardLast4, dto.PaymentMethod)
        };

        await repository.CheckoutBasket(draft, cancellationToken);

        return new CheckoutBasketResult(true);
    }
}
