using System.Security.Cryptography;
using Payment.API.Cards;

namespace Payment.API.Models;

/// <summary>
/// A card held by the Payment service, found by its token. This is the only
/// place in the system where a card number is stored; every other service sees
/// the token and the last four digits. The security code is never part of it.
/// </summary>
/// <remarks>
/// Created only through <see cref="Issue"/>, so a stored card always has a valid,
/// unexpired number and a token it did not choose itself. Exception messages
/// never contain the card number: they can end up in logs.
/// </remarks>
public class StoredCard
{
    /// <summary>The payment token; random, so it reveals nothing about the card.</summary>
    public string Id { get; private set; } = default!;

    public Guid CustomerId { get; private set; }

    public string CardholderName { get; private set; } = default!;

    /// <summary>Digits only.</summary>
    public string CardNumber { get; private set; } = default!;

    /// <summary>MM/YY.</summary>
    public string Expiration { get; private set; } = default!;

    public string CardLast4 { get; private set; } = default!;

    public DateTimeOffset CreatedOnUtc { get; private set; }

    // For Marten.
    private StoredCard()
    {
    }

    public static StoredCard Issue(
        Guid customerId,
        string cardholderName,
        string cardNumber,
        string expiration,
        DateTimeOffset nowUtc)
    {
        if (customerId == Guid.Empty)
        {
            throw new ArgumentException("A card must belong to a customer.", nameof(customerId));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(cardholderName);
        ArgumentNullException.ThrowIfNull(cardNumber);
        ArgumentNullException.ThrowIfNull(expiration);

        if (!CardRules.IsValidNumber(cardNumber))
        {
            throw new ArgumentException("Not a valid card number.", nameof(cardNumber));
        }

        if (!CardRules.IsUnexpired(expiration, nowUtc))
        {
            throw new ArgumentException("The card has expired or the expiry is not MM/YY.", nameof(expiration));
        }

        var digits = CardRules.Normalize(cardNumber);

        return new StoredCard
        {
            Id = NewToken(),
            CustomerId = customerId,
            CardholderName = cardholderName,
            CardNumber = digits,
            Expiration = expiration,
            CardLast4 = digits[^4..],
            CreatedOnUtc = nowUtc
        };
    }

    // Random, not derived from the card: the same card issued twice gets two
    // tokens, and a token cannot be turned back into a card without this database.
    private static string NewToken() =>
        "pt_" + Convert.ToHexString(RandomNumberGenerator.GetBytes(16)).ToLowerInvariant();
}
