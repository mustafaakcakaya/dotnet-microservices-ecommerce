namespace Payment.API.Models;

/// <summary>
/// A card held by the Payment service, found by its token. This is the only
/// place in the system where a card number is stored; every other service sees
/// the token and the last four digits. The security code is never part of it.
/// </summary>
public class StoredCard
{
    /// <summary>The payment token; random, so it reveals nothing about the card.</summary>
    public string Id { get; set; } = default!;

    public Guid CustomerId { get; set; }

    public string CardholderName { get; set; } = default!;

    public string CardNumber { get; set; } = default!;

    /// <summary>MM/YY.</summary>
    public string Expiration { get; set; } = default!;

    public string CardLast4 { get; set; } = default!;

    public DateTimeOffset CreatedOnUtc { get; set; }
}
