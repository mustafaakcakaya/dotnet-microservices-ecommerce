using System.Globalization;

namespace Payment.API.Cards;

/// <summary>Checks on the shape of card details; no network, no issuer.</summary>
public static class CardRules
{
    /// <summary>Removes the spaces and dashes people type between digit groups.</summary>
    public static string Normalize(string cardNumber) =>
        new(cardNumber.Where(c => c is not (' ' or '-')).ToArray());

    /// <summary>12 to 19 digits that pass the Luhn checksum.</summary>
    public static bool IsValidNumber(string cardNumber)
    {
        var digits = Normalize(cardNumber);
        if (digits.Length is < 12 or > 19 || !digits.All(char.IsAsciiDigit))
        {
            return false;
        }

        var sum = 0;
        var doubleIt = false;
        for (var i = digits.Length - 1; i >= 0; i--)
        {
            var digit = digits[i] - '0';
            if (doubleIt)
            {
                digit *= 2;
                if (digit > 9)
                {
                    digit -= 9;
                }
            }

            sum += digit;
            doubleIt = !doubleIt;
        }

        return sum % 10 == 0;
    }

    /// <summary>MM/YY, valid through the last day of that month.</summary>
    public static bool IsUnexpired(string expiration, DateTimeOffset nowUtc)
    {
        if (!DateTime.TryParseExact(expiration, "MM/yy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var month))
        {
            return false;
        }

        var firstDayAfter = new DateTimeOffset(month.Year, month.Month, 1, 0, 0, 0, TimeSpan.Zero).AddMonths(1);
        return nowUtc < firstDayAfter;
    }
}
