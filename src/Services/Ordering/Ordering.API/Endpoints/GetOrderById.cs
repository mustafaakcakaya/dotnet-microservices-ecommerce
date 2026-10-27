using Carter;
using MediatR;
using Ordering.Application.Dtos;
using Ordering.Application.Orders.Queries.GetOrderById;

namespace Ordering.API.Endpoints;

public record GetOrderByIdResponse(OrderDto Order);

public class GetOrderById : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet("/orders/{id:guid}", async (
            Guid id,
            ISender sender,
            CancellationToken cancellationToken) =>
        {
            var result = await sender.Send(
                new GetOrderByIdQuery(id),
                cancellationToken);

            return Results.Ok(new GetOrderByIdResponse(result.Order));
        })
        .WithName("GetOrderById")
        .Produces<GetOrderByIdResponse>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .WithSummary("Get Order By Id")
        .WithDescription("Get a single order by its id");
    }
}
