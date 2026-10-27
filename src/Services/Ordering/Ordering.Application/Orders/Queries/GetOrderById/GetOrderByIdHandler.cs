using BuildingBlocks.CQRS;
using Microsoft.EntityFrameworkCore;
using Ordering.Application.Data;
using Ordering.Application.Exceptions;
using Ordering.Application.Extensions;
using Ordering.Domain.ValueObjects;

namespace Ordering.Application.Orders.Queries.GetOrderById;

public class GetOrderByIdHandler(IApplicationDbContext dbContext)
    : IQueryHandler<GetOrderByIdQuery, GetOrderByIdResult>
{
    public async Task<GetOrderByIdResult> Handle(
        GetOrderByIdQuery query,
        CancellationToken cancellationToken)
    {
        var orderId = OrderId.Of(query.Id);
        var order = await dbContext.Orders
            .Include(order => order.OrderItems)
            .AsNoTracking()
            .SingleOrDefaultAsync(order => order.Id == orderId, cancellationToken)
            ?? throw new OrderNotFoundException(query.Id);

        return new GetOrderByIdResult(order.ToOrderDto());
    }
}
