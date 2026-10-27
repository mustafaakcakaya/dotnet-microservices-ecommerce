namespace Basket.API.Basket.GetBasket;

public record GetBasketResponse(ShoppingCartDto Cart);

public class GetBasketEndpoint  : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet("/basket/{userName}", async (string userName, ISender sender) =>
        {
            var result = await sender.Send(new GetBasketQuery(userName));

            var response = new GetBasketResponse(ShoppingCartDto.From(result.Cart));

            return Results.Ok(response);
        })
        .WithName("GetBasketByUserName")
        .Produces<GetBasketResponse>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status400BadRequest)
        .WithSummary("Get a basket")
        .WithDescription("Get the basket for the specified user");
    }
}
