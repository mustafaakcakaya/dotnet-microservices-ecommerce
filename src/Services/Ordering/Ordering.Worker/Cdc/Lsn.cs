namespace Ordering.Worker.Cdc;

/// <summary>
/// Helpers for SQL Server log sequence numbers (binary(10) values).
/// </summary>
public static class Lsn
{
    public const int Length = 10;

    /// <summary>Lexicographic unsigned comparison, matching SQL Server binary ordering.</summary>
    public static int Compare(byte[] left, byte[] right)
    {
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);

        for (var i = 0; i < Length; i++)
        {
            var diff = left[i].CompareTo(right[i]);
            if (diff != 0)
            {
                return diff;
            }
        }

        return 0;
    }

    public static bool IsZero(byte[]? lsn) => lsn is null || lsn.All(b => b == 0);

    public static string ToHex(byte[]? lsn) => lsn is null ? "<null>" : $"0x{Convert.ToHexString(lsn)}";
}
