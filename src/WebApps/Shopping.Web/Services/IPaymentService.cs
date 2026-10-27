namespace Shopping.Web.Services;

public interface IPaymentService
{
    [Post("/payment-service/payment/tokens")]
    Task<TokenizeCardResponse> TokenizeCard(
        TokenizeCardRequest request,
        CancellationToken cancellationToken = default);
}
