using System.Security.Cryptography;

namespace Payment.API.Cards.TokenizeCard;

public record TokenizeCardCommand(
    Guid CustomerId,
    string CardholderName,
    string CardNumber,
    string Expiration,
    string Cvv) : ICommand<TokenizeCardResult>;

public record TokenizeCardResult(string PaymentToken, string CardLast4);

/// <remarks>
/// Messages never echo the submitted value ({PropertyValue}): validation errors
/// are logged and returned, and a card number must not end up in either.
/// </remarks>
public class TokenizeCardCommandValidator : AbstractValidator<TokenizeCardCommand>
{
    public TokenizeCardCommandValidator(TimeProvider timeProvider)
    {
        RuleFor(x => x.CustomerId).NotEmpty().WithMessage("CustomerId is required");

        RuleFor(x => x.CardholderName).NotEmpty().MaximumLength(50)
            .WithMessage("CardholderName is required (at most 50 characters)");

        RuleFor(x => x.CardNumber)
            .Must(number => number is not null && CardRules.IsValidNumber(number))
            .WithMessage("CardNumber is not a valid card number");

        RuleFor(x => x.Expiration)
            .Must(expiration => expiration is not null && CardRules.IsUnexpired(expiration, timeProvider.GetUtcNow()))
            .WithMessage("Expiration must be MM/YY and not in the past");

        RuleFor(x => x.Cvv)
            .Matches("^[0-9]{3,4}$")
            .WithMessage("Cvv must be 3 or 4 digits");
    }
}

/// <summary>
/// Stores the card and hands back a token for it. The security code is checked
/// for shape and then dropped - it is not stored, logged or returned.
/// </summary>
public class TokenizeCardCommandHandler(IDocumentSession session, TimeProvider timeProvider)
    : ICommandHandler<TokenizeCardCommand, TokenizeCardResult>
{
    public async Task<TokenizeCardResult> Handle(TokenizeCardCommand command, CancellationToken cancellationToken)
    {
        var cardNumber = CardRules.Normalize(command.CardNumber);

        var card = new StoredCard
        {
            Id = NewToken(),
            CustomerId = command.CustomerId,
            CardholderName = command.CardholderName,
            CardNumber = cardNumber,
            Expiration = command.Expiration,
            CardLast4 = cardNumber[^4..],
            CreatedOnUtc = timeProvider.GetUtcNow()
        };

        session.Insert(card);
        await session.SaveChangesAsync(cancellationToken);

        return new TokenizeCardResult(card.Id, card.CardLast4);
    }

    // Random, not derived from the card: the same card tokenized twice gets two
    // tokens, and a token cannot be turned back into a card without this database.
    private static string NewToken() =>
        "pt_" + Convert.ToHexString(RandomNumberGenerator.GetBytes(16)).ToLowerInvariant();
}
