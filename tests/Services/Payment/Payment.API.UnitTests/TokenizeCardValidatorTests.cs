using Payment.API.Cards.TokenizeCard;

namespace Payment.API.UnitTests;

public sealed class TokenizeCardValidatorTests
{
    private const string CardNumber = "4111111111111112";
    private const string Cvv = "987";

    private readonly TokenizeCardCommandValidator _validator =
        new(new FixedTimeProvider(new DateTimeOffset(2026, 10, 26, 0, 0, 0, TimeSpan.Zero)));

    [Fact]
    public void ValidCard_Passes()
    {
        var result = _validator.Validate(Command() with { CardNumber = "4111111111111111" });

        Assert.True(result.IsValid);
    }

    // Validation errors are logged and returned to the caller, so they must never
    // carry the submitted card number or security code.
    [Fact]
    public void Errors_NeverEchoTheSubmittedValues()
    {
        var result = _validator.Validate(Command() with { Cvv = "98765" });

        Assert.False(result.IsValid);
        Assert.All(result.Errors, error =>
        {
            Assert.DoesNotContain(CardNumber, error.ErrorMessage);
            Assert.DoesNotContain("98765", error.ErrorMessage);
        });
    }

    [Fact]
    public void ExpiredCard_Fails()
    {
        var result = _validator.Validate(Command() with { CardNumber = "4111111111111111", Expiration = "09/26" });

        Assert.Contains(result.Errors, error => error.PropertyName == nameof(TokenizeCardCommand.Expiration));
    }

    private static TokenizeCardCommand Command() =>
        new(Guid.NewGuid(), "Mustafa Akcakaya", CardNumber, "12/28", Cvv);
}

internal sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => utcNow;
}
