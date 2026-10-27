namespace Shopping.Web.Models.Ordering;

public sealed record OrderModel(
    Guid Id,
    Guid CustomerId,
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
    string PaymentToken,
    string CardLast4,
    int PaymentMethod);

public enum OrderStatus
{
    Draft = 1,
    Pending = 2,
    Completed = 3,
    Cancelled = 4
}

public static class OrderStatusExtensions
{
    /// <summary>Bootstrap 5.1 badge colour for a status.</summary>
    public static string BadgeClass(this OrderStatus status) => status switch
    {
        OrderStatus.Pending => "bg-warning text-dark",
        OrderStatus.Completed => "bg-success",
        OrderStatus.Cancelled => "bg-danger",
        _ => "bg-secondary"
    };

    public static string DisplayName(this OrderStatus status) => status switch
    {
        OrderStatus.Draft => "Taslak",
        OrderStatus.Pending => "Hazırlanıyor",
        OrderStatus.Completed => "Tamamlandı",
        OrderStatus.Cancelled => "İptal Edildi",
        _ => status.ToString()
    };
}

public sealed record GetOrdersResponse(PaginatedResult<OrderModel> Orders);
public sealed record GetOrderByIdResponse(OrderModel Order);
public sealed record GetOrdersByCustomerResponse(IEnumerable<OrderModel> Orders);
