using System.Net;

namespace Shopping.Web.Services;

public interface IBasketService
{
    [Get("/basket-service/basket/{userName}")]
    Task<GetBasketResponse> GetBasket(string userName, CancellationToken cancellationToken = default);

    [Post("/basket-service/basket")]
    Task<StoreBasketResponse> StoreBasket(
        StoreBasketRequest request,
        CancellationToken cancellationToken = default);

    [Delete("/basket-service/basket/{userName}")]
    Task<DeleteBasketResponse> DeleteBasket(string userName, CancellationToken cancellationToken = default);

    [Post("/basket-service/basket/checkout")]
    Task<CheckoutBasketResponse> CheckoutBasket(
        CheckoutBasketRequest request,
        CancellationToken cancellationToken = default);

    async Task<ShoppingCartModel> LoadUserBasket(
        string userName,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await GetBasket(userName, cancellationToken);
            return response.Cart;
        }
        catch (ApiException exception) when (exception.StatusCode == HttpStatusCode.NotFound)
        {
            return new ShoppingCartModel { UserName = userName };
        }
    }
}
