namespace DotNetDifferentialEvolution.GPU.Test.Builder;

/// <summary>
/// The χ² distribution for an integer number of degrees of freedom, computed here rather than
/// typed from a table: the CDF is the regularized lower incomplete gamma function
/// <c>P(df/2, x/2)</c>, summed as its power series, and the quantile is found by bisection.
/// Γ of an integer or half-integer argument is the exact recurrence from Γ(1) = 1 and
/// Γ(1/2) = √π, so no approximation coefficients are involved.
/// </summary>
internal static class ChiSquared
{
    /// <summary>Pearson's statistic, <c>Σ (observed − expected)² / expected</c>.</summary>
    /// <param name="observed">The count in each bin.</param>
    /// <param name="expected">The expected count, the same in every bin.</param>
    /// <returns>The statistic.</returns>
    public static double Statistic(ReadOnlySpan<int> observed, double expected)
    {
        var sum = 0.0;
        foreach (var count in observed)
        {
            var deviation = count - expected;
            sum += deviation * deviation / expected;
        }

        return sum;
    }

    /// <summary>The CDF of χ² with <paramref name="degreesOfFreedom"/> degrees of freedom.</summary>
    /// <param name="x">The argument, ≥ 0.</param>
    /// <param name="degreesOfFreedom">The degrees of freedom, ≥ 1.</param>
    /// <returns><c>P(χ² ≤ x)</c>.</returns>
    public static double Cdf(double x, int degreesOfFreedom)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(degreesOfFreedom, 1);
        return x <= 0.0 ? 0.0 : RegularizedLowerGamma(degreesOfFreedom / 2.0, x / 2.0);
    }

    /// <summary>The quantile of χ²: the x at which the CDF equals <paramref name="probability"/>.</summary>
    /// <param name="probability">The probability, in (0, 1).</param>
    /// <param name="degreesOfFreedom">The degrees of freedom, ≥ 1.</param>
    /// <returns>The quantile, to the resolution of bisection in <see cref="double"/>.</returns>
    public static double Quantile(double probability, int degreesOfFreedom)
    {
        if (probability is not (> 0.0 and < 1.0))
        {
            throw new ArgumentOutOfRangeException(nameof(probability), probability, "The probability must be in (0, 1).");
        }

        var low = 0.0;
        var high = 1.0;
        while (Cdf(high, degreesOfFreedom) < probability)
        {
            low = high;
            high *= 2.0;
        }

        for (var iteration = 0; iteration < 200; iteration++)
        {
            var middle = (low + high) / 2.0;
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

        return (low + high) / 2.0;
    }

    /// <summary>ln Γ(a) for an integer or half-integer <paramref name="a"/>, by the exact recurrence Γ(a) = (a − 1)·Γ(a − 1).</summary>
    /// <param name="a">The argument, a positive multiple of 1/2.</param>
    /// <returns>ln Γ(a).</returns>
    public static double LogGammaOfHalfInteger(double a)
    {
        var twice = a * 2.0;
        if (twice < 1.0 || Math.Floor(twice) != twice)
        {
            throw new ArgumentOutOfRangeException(nameof(a), a, "Only positive multiples of 1/2 are supported.");
        }

        // Γ(1) = 1, Γ(1/2) = √π; walk up in steps of 1.
        var current = Math.Floor(a) == a ? 1.0 : 0.5;
        var logGamma = current == 1.0 ? 0.0 : 0.5 * Math.Log(Math.PI);
        while (current < a)
        {
            logGamma += Math.Log(current);
            current += 1.0;
        }

        return logGamma;
    }

    /// <summary>
    /// P(a, x) = x^a·e^(−x)·Σ_{n≥0} x^n / Γ(a + n + 1). Every term is positive, so the sum has no
    /// cancellation; it converges for every x and is accurate in the range the tests use.
    /// </summary>
    private static double RegularizedLowerGamma(double a, double x)
    {
        var term = Math.Exp(a * Math.Log(x) - x - LogGammaOfHalfInteger(a + 1.0));
        var sum = term;
        for (var n = 1; n < 10_000; n++)
        {
            term *= x / (a + n);
            sum += term;
            if (term < sum * 1e-17)
            {
                break;
            }
        }

        return Math.Min(sum, 1.0);
    }
}
