namespace DotNetDifferentialEvolution.GPU.Test.Random;

/// <summary>
/// The χ² distribution for the uniformity checks (ACCEPTANCE.md, checks 1b and 3b): the statistic
/// of a histogram, the CDF and the quantile. The quantile is computed, never typed: the CDF is the
/// regularized lower incomplete gamma function <c>P(df/2, x/2)</c> (series below <c>a + 1</c>,
/// Lentz's continued fraction above, as in Numerical Recipes §6.2), and the quantile is found by
/// bisection on it. Its positive controls are in <see cref="ChiSquaredTests"/>.
/// </summary>
internal static class ChiSquared
{
    private const double Epsilon = 1e-15;
    private const double Tiny = 1e-300;
    private const int MaxIterations = 1_000_000;

    /// <summary>The χ² statistic of observed counts against one expected count per bin.</summary>
    /// <param name="observed">The counts.</param>
    /// <param name="expected">The expected count of every bin.</param>
    /// <returns><c>Σ (o − e)² / e</c>.</returns>
    public static double Statistic(ReadOnlySpan<long> observed, double expected)
    {
        var sum = 0.0;
        foreach (var count in observed)
        {
            var difference = count - expected;
            sum += difference * difference / expected;
        }

        return sum;
    }

    /// <summary>The CDF of the χ² distribution with <paramref name="degreesOfFreedom"/> degrees of freedom.</summary>
    /// <param name="x">The point, at least 0.</param>
    /// <param name="degreesOfFreedom">The degrees of freedom, at least 1.</param>
    /// <returns><c>P(df/2, x/2)</c>.</returns>
    public static double Cdf(double x, int degreesOfFreedom)
    {
        var a = degreesOfFreedom / 2.0;
        var halfX = x / 2.0;
        return halfX <= 0.0 ? 0.0
            : halfX < a + 1.0 ? LowerSeries(a, halfX, degreesOfFreedom)
            : 1.0 - UpperFraction(a, halfX, degreesOfFreedom);
    }

    /// <summary>The <paramref name="probability"/> quantile, by bisection on <see cref="Cdf"/> to the last bit.</summary>
    /// <param name="probability">The probability, in (0, 1).</param>
    /// <param name="degreesOfFreedom">The degrees of freedom, at least 1.</param>
    /// <returns>The <c>x</c> with <c>Cdf(x) = probability</c>.</returns>
    public static double Quantile(double probability, int degreesOfFreedom)
    {
        var low = 0.0;
        var high = Math.Max(1.0, degreesOfFreedom);
        while (Cdf(high, degreesOfFreedom) < probability)
        {
            low = high;
            high *= 2.0;
        }

        for (var step = 0; step < 2000; step++)
        {
            var middle = low + (high - low) / 2.0;
            if (middle <= low || middle >= high)
            {
                break;
            }

            if (Cdf(middle, degreesOfFreedom) < probability)
            {
                low = middle;
            }
            else
            {
                high = middle;
            }
        }

        return low + (high - low) / 2.0;
    }

    /// <summary>
    /// <c>ln Γ(df/2)</c>, exactly from the recurrence: <c>df/2</c> is an integer or a half-integer,
    /// <c>Γ(n) = (n − 1)!</c> and <c>Γ(n + ½) = √π · Π (k − ½)</c>. No approximation of Γ is involved.
    /// </summary>
    /// <param name="degreesOfFreedom">The degrees of freedom, at least 1.</param>
    /// <returns>The logarithm of Γ(df/2).</returns>
    public static double LogGammaOfHalf(int degreesOfFreedom)
    {
        var sum = 0.0;
        if (degreesOfFreedom % 2 == 0)
        {
            for (var k = 2; k < degreesOfFreedom / 2; k++)
            {
                sum += Math.Log(k);
            }

            return sum;
        }

        sum = 0.5 * Math.Log(Math.PI);
        for (var k = 1; k <= degreesOfFreedom / 2; k++)
        {
            sum += Math.Log(k - 0.5);
        }

        return sum;
    }

    private static double Prefactor(double a, double x, int degreesOfFreedom) =>
        Math.Exp(-x + a * Math.Log(x) - LogGammaOfHalf(degreesOfFreedom));

    private static double LowerSeries(double a, double x, int degreesOfFreedom)
    {
        var term = 1.0 / a;
        var sum = term;
        var denominator = a;
        for (var n = 0; n < MaxIterations; n++)
        {
            denominator += 1.0;
            term *= x / denominator;
            sum += term;
            if (Math.Abs(term) < Math.Abs(sum) * Epsilon)
            {
                break;
            }
        }

        return sum * Prefactor(a, x, degreesOfFreedom);
    }

    private static double UpperFraction(double a, double x, int degreesOfFreedom)
    {
        var b = x + 1.0 - a;
        var c = 1.0 / Tiny;
        var d = 1.0 / b;
        var h = d;
        for (var i = 1; i < MaxIterations; i++)
        {
            var an = -i * (i - a);
            b += 2.0;
            d = an * d + b;
            if (Math.Abs(d) < Tiny)
            {
                d = Tiny;
            }

            c = b + an / c;
            if (Math.Abs(c) < Tiny)
            {
                c = Tiny;
            }

            d = 1.0 / d;
            var delta = d * c;
            h *= delta;
            if (Math.Abs(delta - 1.0) < Epsilon)
            {
                break;
            }
        }

        return h * Prefactor(a, x, degreesOfFreedom);
    }
}
