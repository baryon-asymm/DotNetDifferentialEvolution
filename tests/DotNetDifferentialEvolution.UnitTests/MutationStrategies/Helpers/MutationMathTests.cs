using DotNetDifferentialEvolution.MutationStrategies.Helpers;
using DotNetDifferentialEvolution.RandomProviders;

namespace DotNetDifferentialEvolution.UnitTests.MutationStrategies.Helpers;

/// <summary>
/// Verifies the SIMD-accelerated vector arithmetic against a plain scalar reference. The
/// genome sizes deliberately straddle <see cref="System.Numerics.Vector{T}.Count"/> so both
/// the vectorized body and the scalar tail are exercised.
/// </summary>
[Trait("Category", "Unit")]
public class MutationMathTests
{
    private const double Precision = 1e-12;

    /// <summary>
    /// The genome sizes every theory runs at: small literal sizes, the sizes on either side of one
    /// and two hardware vector widths, and a larger odd size, without duplicates.
    /// </summary>
    /// <returns>The distinct genome sizes for this machine's vector width.</returns>
    public static TheoryData<int> GenomeSizes()
    {
        var vectorWidth = System.Numerics.Vector<double>.Count;

        // Distinct(), because the sizes are partly literal and partly derived from the hardware
        // vector width: at width 4 the literal 3 and vectorWidth - 1 are the same number, and xUnit
        // silently drops the second case of a duplicate theory ID rather than failing. The set that
        // actually runs would otherwise depend on which machine ran it.
        var sizes = new TheoryData<int>();
        foreach (var size in new[] { 1, 2, 3, vectorWidth - 1, vectorWidth, vectorWidth + 1, 2 * vectorWidth, 2 * vectorWidth + 3, 37 }
                     .Where(size => size >= 1)
                     .Distinct())
        {
            sizes.Add(size);
        }

        return sizes;
    }

    /// <summary>
    /// <c>AssignBasePlusScaledDifference</c> writes <c>base + F * (minuend - subtrahend)</c> for every
    /// gene, matching the scalar formula.
    /// </summary>
    /// <param name="genomeSize">The vector length.</param>
    [Theory]
    [MemberData(nameof(GenomeSizes))]
    public void AssignBasePlusScaledDifferenceMatchesScalarReference(
        int genomeSize)
    {
        var random = new SeededRandomProvider(genomeSize * 7919);
        var baseVector = RandomVector(random, genomeSize);
        var minuend = RandomVector(random, genomeSize);
        var subtrahend = RandomVector(random, genomeSize);
        const double force = 0.673;

        var actual = new double[genomeSize];
        MutationMath.AssignBasePlusScaledDifference(actual, baseVector, minuend, subtrahend, force);

        for (var i = 0; i < genomeSize; i++)
        {
            Assert.Equal(baseVector[i] + force * (minuend[i] - subtrahend[i]), actual[i], Precision);
        }
    }

    /// <summary>
    /// <c>AddScaledDifference</c> adds <c>F * (minuend - subtrahend)</c> onto the existing destination
    /// values, matching the scalar formula.
    /// </summary>
    /// <param name="genomeSize">The vector length.</param>
    [Theory]
    [MemberData(nameof(GenomeSizes))]
    public void AddScaledDifferenceAccumulatesOntoDestination(
        int genomeSize)
    {
        var random = new SeededRandomProvider(genomeSize * 104729);
        var initial = RandomVector(random, genomeSize);
        var minuend = RandomVector(random, genomeSize);
        var subtrahend = RandomVector(random, genomeSize);
        const double force = 0.42;

        var actual = (double[])initial.Clone();
        MutationMath.AddScaledDifference(actual, minuend, subtrahend, force);

        for (var i = 0; i < genomeSize; i++)
        {
            Assert.Equal(initial[i] + force * (minuend[i] - subtrahend[i]), actual[i], Precision);
        }
    }

    /// <summary>
    /// <c>AssignCurrentToTarget</c> writes <c>current + F * (target - current)</c> for every gene,
    /// matching the scalar formula.
    /// </summary>
    /// <param name="genomeSize">The vector length.</param>
    [Theory]
    [MemberData(nameof(GenomeSizes))]
    public void AssignCurrentToTargetMovesCurrentTowardTarget(
        int genomeSize)
    {
        var random = new SeededRandomProvider(genomeSize * 1299709);
        var current = RandomVector(random, genomeSize);
        var target = RandomVector(random, genomeSize);
        const double force = 0.9;

        var actual = new double[genomeSize];
        MutationMath.AssignCurrentToTarget(actual, current, target, force);

        for (var i = 0; i < genomeSize; i++)
        {
            Assert.Equal(current[i] + force * (target[i] - current[i]), actual[i], Precision);
        }
    }

    /// <summary>
    /// With F = 0, the current-to-target step leaves the current vector unchanged.
    /// </summary>
    [Fact]
    public void AssignCurrentToTargetWithForceZeroYieldsCurrent()
    {
        double[] current = [1.0, -2.0, 3.5];
        double[] target = [10.0, 10.0, 10.0];

        var actual = new double[current.Length];
        MutationMath.AssignCurrentToTarget(actual, current, target, mutationForce: 0.0);

        Assert.Equal(current, actual);
    }

    /// <summary>
    /// With F = 1, the current-to-target step lands on the target vector.
    /// </summary>
    [Fact]
    public void AssignCurrentToTargetWithForceOneYieldsTarget()
    {
        double[] current = [1.0, -2.0, 3.5];
        double[] target = [10.0, 10.0, 10.0];

        var actual = new double[current.Length];
        MutationMath.AssignCurrentToTarget(actual, current, target, mutationForce: 1.0);

        for (var i = 0; i < target.Length; i++)
        {
            Assert.Equal(target[i], actual[i], Precision);
        }
    }

    private static double[] RandomVector(
        SeededRandomProvider random,
        int length)
    {
        var vector = new double[length];
        for (var i = 0; i < length; i++)
        {
            vector[i] = random.NextDouble() * 20.0 - 10.0;
        }

        return vector;
    }
}
