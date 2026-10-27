using Payment.API.Cards;

namespace Payment.API.UnitTests;

public sealed class CardRulesTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 26, 12, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData("4111111111111111")]
    [InlineData("5555555555554444")]
    [InlineData("4111 1111 1111 1111")]
    [InlineData("4111-1111-1111-1111")]
    public void IsValidNumber_AcceptsLuhnValidNumbers(string number) =>
        Assert.True(CardRules.IsValidNumber(number));

    [Theory]
    [InlineData("4111111111111112")] // checksum off by one
    [InlineData("41111111111")] // too short
    [InlineData("41111111111111111111")] // too long
    [InlineData("4111x11111111111")]
    [InlineData("")]
    public void IsValidNumber_RejectsEverythingElse(string number) =>
        Assert.False(CardRules.IsValidNumber(number));

    [Theory]
    [InlineData("10/26")] // valid through the end of the current month
    [InlineData("01/27")]
    public void IsUnexpired_AcceptsThisMonthAndLater(string expiration) =>
        Assert.True(CardRules.IsUnexpired(expiration, Now));

    [Theory]
    [InlineData("09/26")]
    [InlineData("13/27")]
    [InlineData("1/27")]
    [InlineData("2027-01")]
    public void IsUnexpired_RejectsPastOrMalformed(string expiration) =>
        Assert.False(CardRules.IsUnexpired(expiration, Now));
}
