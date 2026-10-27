namespace Payment.API.Cards.TokenizeCard;

public record TokenizeCardRequest(
    Guid CustomerId,
    string CardholderName,
    string CardNumber,
    string Expiration,
    string Cvv);

public record TokenizeCardResponse(string PaymentToken, string CardLast4);

public class TokenizeCardEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost("/payment/tokens", async (TokenizeCardRequest request, ISender sender, CancellationToken ct) =>
            {
                var command = new TokenizeCardCommand(
                    request.CustomerId,
                    request.CardholderName,
                    request.CardNumber,
                    request.Expiration,
                    request.Cvv);

                var result = await sender.Send(command, ct);

                return Results.Ok(new TokenizeCardResponse(result.PaymentToken, result.CardLast4));
            })
            .WithName("TokenizeCard")
            .Produces<TokenizeCardResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .WithSummary("Tokenize a card")
            .WithDescription("Stores the card and returns a token that other services use instead of the card details.");
    }
}
