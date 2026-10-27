namespace Ordering.Domain.ValueObjects;

/// <summary>
/// How an order is paid for. The card itself lives in the Payment service;
/// an order keeps only the token that refers to it and the last four digits
/// to show the customer which card was used.
/// </summary>
public record Payment
{
    public string PaymentToken { get; } = default!;
    public string CardLast4 { get; } = default!;
    public int PaymentMethod { get; } = default!;

    protected Payment()
    {
    }

    private Payment(string paymentToken, string cardLast4, int paymentMethod)
    {
        PaymentToken = paymentToken;
        CardLast4 = cardLast4;
        PaymentMethod = paymentMethod;
    }

    public static Payment Of(string paymentToken, string cardLast4, int paymentMethod)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(paymentToken);
        ArgumentException.ThrowIfNullOrWhiteSpace(cardLast4);

        if (cardLast4.Length != 4 || !cardLast4.All(char.IsAsciiDigit))
        {
            throw new ArgumentException("Must be the last four digits of the card.", nameof(cardLast4));
        }

        return new Payment(paymentToken, cardLast4, paymentMethod);
    }
}
