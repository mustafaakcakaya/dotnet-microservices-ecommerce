namespace Shopping.Web.Models.Payment;

public sealed record TokenizeCardRequest(
    Guid CustomerId,
    string CardholderName,
    string CardNumber,
    string Expiration,
    string Cvv);

public sealed record TokenizeCardResponse(string PaymentToken, string CardLast4);
