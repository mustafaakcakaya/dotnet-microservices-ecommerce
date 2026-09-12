namespace Shopping.Web.Models.Ordering;

public sealed record OrderModel(
    Guid Id,
    Guid CustomerId,
    string OrderName,
    AddressModel ShippingAddress,
    AddressModel BillingAddress,
    PaymentModel Payment,
    OrderStatus Status,
    List<OrderItemModel> OrderItems);

public sealed record OrderItemModel(Guid OrderId, Guid ProductId, int Quantity, decimal Price);

public sealed record AddressModel(
    string FirstName,
    string LastName,
    string EmailAddress,
    string AddressLine,
    string Country,
    string State,
    string ZipCode);

public sealed record PaymentModel(
    string CardName,
    string CardNumber,
    string Expiration,
    string Cvv,
    int PaymentMethod);

public enum OrderStatus
{
    Draft = 1,
    Pending = 2,
    Completed = 3,
    Cancelled = 4
}

public sealed record GetOrdersResponse(PaginatedResult<OrderModel> Orders);
public sealed record GetOrdersByNameResponse(IEnumerable<OrderModel> Orders);
public sealed record GetOrdersByCustomerResponse(IEnumerable<OrderModel> Orders);
