namespace Ordering.Application.Dtos;

/// <param name="PaymentToken">Token issued by the Payment service; card details never reach Ordering.</param>
public record PaymentDto(
    string PaymentToken,
    string CardLast4,
    int PaymentMethod);
