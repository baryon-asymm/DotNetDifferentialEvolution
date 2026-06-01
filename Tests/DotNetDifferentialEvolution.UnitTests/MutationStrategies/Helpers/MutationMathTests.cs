using DotNetDifferentialEvolution.MutationStrategies.Helpers;

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

    public static IEnumerable<object[]> GenomeSizes()
    {
        var vectorWidth = System.Numerics.Vector<double>.Count;
        foreach (var size in new[] { 1, 2, 3, vectorWidth - 1, vectorWidth, vectorWidth + 1, 2 * vectorWidth, 2 * vectorWidth + 3, 37 })
            if (size >= 1)
                yield return [size];
    }

    [Theory]
    [MemberData(nameof(GenomeSizes))]
    public void AssignBasePlusScaledDifference_MatchesScalarReference(
        int genomeSize)
    {
        var random = new Random(genomeSize * 7919);
        var baseVector = RandomVector(random, genomeSize);
        var minuend = RandomVector(random, genomeSize);
        var subtrahend = RandomVector(random, genomeSize);
        const double force = 0.673;

        var actual = new double[genomeSize];
        MutationMath.AssignBasePlusScaledDifference(actual, baseVector, minuend, subtrahend, force);

        for (int i = 0; i < genomeSize; i++)
            Assert.Equal(baseVector[i] + force * (minuend[i] - subtrahend[i]), actual[i], Precision);
    }

    [Theory]
    [MemberData(nameof(GenomeSizes))]
    public void AddScaledDifference_AccumulatesOntoDestination(
        int genomeSize)
    {
        var random = new Random(genomeSize * 104729);
        var initial = RandomVector(random, genomeSize);
        var minuend = RandomVector(random, genomeSize);
        var subtrahend = RandomVector(random, genomeSize);
        const double force = 0.42;

        var actual = (double[])initial.Clone();
        MutationMath.AddScaledDifference(actual, minuend, subtrahend, force);

        for (int i = 0; i < genomeSize; i++)
            Assert.Equal(initial[i] + force * (minuend[i] - subtrahend[i]), actual[i], Precision);
    }

    [Theory]
    [MemberData(nameof(GenomeSizes))]
    public void AssignCurrentToTarget_MovesCurrentTowardTarget(
        int genomeSize)
    {
        var random = new Random(genomeSize * 1299709);
        var current = RandomVector(random, genomeSize);
        var target = RandomVector(random, genomeSize);
        const double force = 0.9;

        var actual = new double[genomeSize];
        MutationMath.AssignCurrentToTarget(actual, current, target, force);

        for (int i = 0; i < genomeSize; i++)
            Assert.Equal(current[i] + force * (target[i] - current[i]), actual[i], Precision);
    }

    [Fact]
    public void AssignCurrentToTarget_WithForceZero_YieldsCurrent()
    {
        double[] current = [1.0, -2.0, 3.5];
        double[] target = [10.0, 10.0, 10.0];

        var actual = new double[current.Length];
        MutationMath.AssignCurrentToTarget(actual, current, target, mutationForce: 0.0);

        Assert.Equal(current, actual);
    }

    [Fact]
    public void AssignCurrentToTarget_WithForceOne_YieldsTarget()
    {
        double[] current = [1.0, -2.0, 3.5];
        double[] target = [10.0, 10.0, 10.0];

        var actual = new double[current.Length];
        MutationMath.AssignCurrentToTarget(actual, current, target, mutationForce: 1.0);

        for (int i = 0; i < target.Length; i++)
            Assert.Equal(target[i], actual[i], Precision);
    }

    private static double[] RandomVector(
        Random random,
        int length)
    {
        var vector = new double[length];
        for (int i = 0; i < length; i++)
            vector[i] = random.NextDouble() * 20.0 - 10.0;

        return vector;
    }
}
