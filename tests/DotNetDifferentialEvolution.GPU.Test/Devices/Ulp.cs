namespace DotNetDifferentialEvolution.GPU.Test.Devices;

/// <summary>
/// The distance between two doubles in units in the last place: how many representable doubles
/// lie between them, counted on the IEEE 754 bit patterns
/// (<see cref="BitConverter.DoubleToInt64Bits"/>) mapped to a monotone integer scale, so the
/// count is right across zero and across exponent boundaries.
/// </summary>
internal static class Ulp
{
    /// <summary>The number of ULP steps from <paramref name="a"/> to <paramref name="b"/>.</summary>
    /// <param name="a">One value.</param>
    /// <param name="b">The other value.</param>
    /// <returns>0 for equal values (+0 and −0 included); <see cref="ulong.MaxValue"/> when either is <see cref="double.NaN"/>.</returns>
    public static ulong Distance(double a, double b)
    {
        if (double.IsNaN(a) || double.IsNaN(b))
        {
            return ulong.MaxValue;
        }

        if (a == b)
        {
            return 0;
        }

        var orderedA = Ordered(a);
        var orderedB = Ordered(b);
        var high = Math.Max(orderedA, orderedB);
        var low = Math.Min(orderedA, orderedB);

        // The true difference is below 2⁶⁴; the wrapped long subtraction reinterpreted as ulong is it.
        return unchecked((ulong)(high - low));
    }

    /// <summary>
    /// Maps a double's bits to a signed integer that increases with the value: non-negative doubles
    /// keep their bits, negative ones are reflected below zero so −0 and +0 meet at 0.
    /// </summary>
    private static long Ordered(double value)
    {
        var bits = BitConverter.DoubleToInt64Bits(value);
        return bits >= 0 ? bits : long.MinValue - bits;
    }
}
