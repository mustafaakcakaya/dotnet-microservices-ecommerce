namespace Basket.API.Basket.GetBasket;

public record GetBasketQuery(string UserName) : IQuery<GetBasketResult>;

public record GetBasketResult(ShoppingCart ShoppingCart);

internal class GetBasketQueryHandler : IQueryHandler<GetBasketQuery, GetBasketResult>
{
    public async Task<GetBasketResult> Handle(GetBasketQuery query, CancellationToken cancellationToken)
    {
        //TODO: Implement the logic to get the basket later.
        //var basket = await _basketRepository.GetBasket(query.UserName);
        //TODO: update cache later.

        return new GetBasketResult(new ShoppingCart("swn"));
    }
}