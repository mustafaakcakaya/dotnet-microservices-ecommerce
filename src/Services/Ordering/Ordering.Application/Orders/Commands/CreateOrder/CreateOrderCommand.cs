using BuildingBlocks.CQRS;
using FluentValidation;
using Ordering.Application.Dtos;

namespace Ordering.Application.Orders.Commands.CreateOrder;

/// <param name="Products">
/// Products the order refers to, as the sender saw them. Any that Ordering does
/// not know yet are added in the same transaction as the order, so an order for a
/// product that exists only in the catalog does not fail on the product key.
/// </param>
public record CreateOrderCommand(OrderDto Order, IReadOnlyList<ProductSnapshotDto>? Products = null)
    : ICommand<CreateOrderResult>;

public record CreateOrderResult(Guid Id);

public class CreateOrderCommandValidator : AbstractValidator<CreateOrderCommand>
{
    public CreateOrderCommandValidator()
    {
        RuleFor(x => x.Order.CustomerId)
            .NotNull()
            .WithMessage("CustomerId is required");

        RuleFor(x => x.Order.OrderItems)
            .NotEmpty()
            .WithMessage("OrderItems should not be empty");
    }
}
