using Ordering.Worker.Cdc;

namespace Ordering.Worker.UnitTests.Cdc;

public sealed class LsnTests
{
    [Fact]
    public void Compare_OrdersLexicographically()
    {
        var low = new byte[] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 1 };
        var high = new byte[] { 0, 0, 0, 0, 0, 0, 0, 0, 1, 0 };

        Assert.True(Lsn.Compare(low, high) < 0);
        Assert.True(Lsn.Compare(high, low) > 0);
        Assert.Equal(0, Lsn.Compare(low, (byte[])low.Clone()));
    }

    [Fact]
    public void Compare_TreatsBytesAsUnsigned()
    {
        var positive = new byte[] { 0x7F, 0, 0, 0, 0, 0, 0, 0, 0, 0 };
        var highBit = new byte[] { 0x80, 0, 0, 0, 0, 0, 0, 0, 0, 0 };

        Assert.True(Lsn.Compare(positive, highBit) < 0);
    }

    [Fact]
    public void IsZero_DetectsNullAndZeroLsns()
    {
        Assert.True(Lsn.IsZero(null));
        Assert.True(Lsn.IsZero(new byte[10]));
        Assert.False(Lsn.IsZero(new byte[] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 1 }));
    }
}
