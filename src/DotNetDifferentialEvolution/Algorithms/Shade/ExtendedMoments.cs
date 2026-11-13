namespace DotNetDifferentialEvolution.Algorithms.Shade;

/// <summary>Positive SHADE moments with a binary exponent independent of the double significand.</summary>
internal struct ExtendedMoments
{
    private const double MinNormal = 2.2250738585072014e-308;
    private const double UnderflowMargin = MinNormal * 9007199254740992.0;

    // Underflow or a device flushing denormals can lose O(N * MinNormal) in a sum.
    // Keep 53 bits of headroom. Also protect a subnormal final quotient.
    internal static bool NeedsRatio(double numerator, double denominator, double totalWeight, int count) =>
        numerator <= Math.Max(count, denominator) * UnderflowMargin || denominator <= totalWeight * UnderflowMargin;

    internal Binary Weight;
    internal Binary Cr;
    internal Binary CrSquared;
    internal Binary F;
    internal Binary FSquared;

    internal void Add(Binary weight, double cr, double f)
    {
        var crossover = Binary.From(cr);
        var force = Binary.From(f);
        var weightedCr = weight.Times(crossover);
        var weightedF = weight.Times(force);
        Weight = Weight.Plus(weight);
        Cr = Cr.Plus(weightedCr);
        CrSquared = CrSquared.Plus(weightedCr.Times(crossover));
        F = F.Plus(weightedF);
        FSquared = FSquared.Plus(weightedF.Times(force));
    }

    /// <summary>A nonnegative significand in [1, 2) and its base-two exponent, or zero.</summary>
    internal readonly record struct Binary(double Mantissa, int Exponent)
    {
        private const long One = 1023L << 52;
        private const long FractionMask = (1L << 52) - 1;

        internal static Binary From(double value)
        {
            var bits = BitConverter.DoubleToInt64Bits(value) & long.MaxValue;
            var field = (int)(bits >> 52);
            if (field == 0)
            {
                // Convert the integer significand exactly, even on devices that flush denormals.
                bits = BitConverter.DoubleToInt64Bits(bits & FractionMask);
                field = (int)(bits >> 52) - 1074;
            }

            return bits == 0 ? default : new Binary(BitConverter.Int64BitsToDouble(One | (bits & FractionMask)), field - 1023);
        }

        internal Binary Times(Binary other) => Normalize(Mantissa * other.Mantissa, Exponent + other.Exponent);

        internal Binary Shift(int places) => new(Mantissa, Exponent + places);

        internal Binary Plus(Binary other)
        {
            if (Mantissa == 0 || other.Mantissa == 0)
            {
                return Mantissa == 0 ? other : this;
            }

            var high = Exponent >= other.Exponent ? this : other;
            var low = Exponent >= other.Exponent ? other : this;
            var gap = high.Exponent - low.Exponent;
            // Beyond 54 bits the smaller term cannot change a binary64 addition.
            return gap > 54 ? high : Normalize(high.Mantissa + low.Mantissa * PowerOfTwo(-gap), high.Exponent);
        }

        internal double Ratio(Binary divisor)
        {
            if (Mantissa == 0)
            {
                return 0;
            }

            var ratio = Mantissa / divisor.Mantissa;
            var exponent = Exponent - divisor.Exponent;
            if (ratio < 1)
            {
                ratio *= 2;
                exponent--;
            }

            if (exponent >= -1022)
            {
                return ratio * PowerOfTwo(exponent);
            }

            // Form subnormal output bits from normal units, round to nearest even, including zero.
            if (exponent < -1075)
            {
                return 0;
            }

            var units = ratio * PowerOfTwo(exponent + 1074);
            var whole = (long)units;
            var fraction = units - whole;
            var roundUp = fraction > 0.5 || (fraction == 0.5 && (whole & 1) != 0);
            return BitConverter.Int64BitsToDouble(roundUp ? whole + 1 : whole);
        }

        private static Binary Normalize(double mantissa, int exponent) =>
            mantissa >= 2 ? new Binary(mantissa * 0.5, exponent + 1) : new Binary(mantissa, exponent);

        private static double PowerOfTwo(int exponent) => BitConverter.Int64BitsToDouble((long)(exponent + 1023) << 52);
    }
}
