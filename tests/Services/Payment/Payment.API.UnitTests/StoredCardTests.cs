using Payment.API.Models;

namespace Payment.API.UnitTests;

public sealed class StoredCardTests
{
    private const string CardNumber = "4111 1111 1111 1111";
    private static readonly DateTimeOffset Now = new(2026, 10, 27, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Issue_KeepsTheDigitsAndTheLastFour()
    {
        var card = Issue(CardNumber);

        Assert.Equal("4111111111111111", card.CardNumber);
        Assert.Equal("1111", card.CardLast4);
        Assert.Equal(Now, card.CreatedOnUtc);
    }

    // Random, not derived from the card, so a token reveals nothing about it.
    [Fact]
    public void Issue_GivesEachCardItsOwnRandomToken()
    {
        var first = Issue(CardNumber);
        var second = Issue(CardNumber);

        Assert.Matches("^pt_[0-9a-f]{32}$", first.Id);
        Assert.NotEqual(first.Id, second.Id);
    }

    // The validator normally stops these first; the card still refuses to exist
    // in an invalid state, and says why without repeating the number.
    [Fact]
    public void Issue_RejectsAnInvalidNumberWithoutEchoingIt()
    {
        var exception = Assert.Throws<ArgumentException>(() => Issue("4111111111111112"));

        Assert.DoesNotContain("4111111111111112", exception.Message);
    }

    [Fact]
    public void Issue_RejectsAnExpiredCard() =>
        Assert.Throws<ArgumentException>(() => Issue(CardNumber, expiration: "09/26"));

    [Fact]
    public void Issue_RequiresACustomer() =>
        Assert.Throws<ArgumentException>(() =>
            StoredCard.Issue(Guid.Empty, "Mustafa Akcakaya", CardNumber, "12/28", Now));

    private static StoredCard Issue(string cardNumber, string expiration = "12/28") =>
        StoredCard.Issue(Guid.NewGuid(), "Mustafa Akcakaya", cardNumber, expiration, Now);
}
