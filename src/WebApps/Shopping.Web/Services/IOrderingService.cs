namespace Shopping.Web.Services;

public interface IOrderingService
{
    [Get("/ordering-service/orders")]
    Task<GetOrdersResponse> GetOrders(
        [AliasAs("pageIndex")] int? pageIndex = 0,
        [AliasAs("pageSize")] int? pageSize = 10,
        CancellationToken cancellationToken = default);

    [Get("/ordering-service/orders/{orderName}")]
    Task<GetOrdersByNameResponse> GetOrdersByName(
        string orderName,
        CancellationToken cancellationToken = default);

    [Get("/ordering-service/orders/customer/{customerId}")]
    Task<GetOrdersByCustomerResponse> GetOrdersByCustomer(
        Guid customerId,
        CancellationToken cancellationToken = default);
}
