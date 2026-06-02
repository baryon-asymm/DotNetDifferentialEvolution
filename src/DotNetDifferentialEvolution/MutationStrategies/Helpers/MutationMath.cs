using System.Numerics;
using System.Runtime.InteropServices;

namespace DotNetDifferentialEvolution.MutationStrategies.Helpers;

/// <summary>
/// SIMD-accelerated vector arithmetic shared by the differential mutation strategies.
/// All operations use <see cref="Vector{T}"/> over <see cref="double"/> with a scalar
/// tail, mirroring the original hand-written loop in the classic strategy.
/// </summary>
internal static class MutationMath
{
    /// <summary>
    /// Computes <c>destination = baseVector + mutationForce * (minuend - subtrahend)</c>.
    /// </summary>
    public static void AssignBasePlusScaledDifference(
        Span<double> destination,
        ReadOnlySpan<double> baseVector,
        ReadOnlySpan<double> minuend,
        ReadOnlySpan<double> subtrahend,
        double mutationForce)
    {
        var genomeSize = destination.Length;
        var handledGenesCount = 0;

        if (Vector<double>.Count <= genomeSize)
        {
            var destinationVectors = MemoryMarshal.Cast<double, Vector<double>>(destination);
            var baseVectors = MemoryMarshal.Cast<double, Vector<double>>(baseVector);
            var minuendVectors = MemoryMarshal.Cast<double, Vector<double>>(minuend);
            var subtrahendVectors = MemoryMarshal.Cast<double, Vector<double>>(subtrahend);

            for (int i = 0; i < destinationVectors.Length; i++)
                destinationVectors[i] = baseVectors[i] + mutationForce * (minuendVectors[i] - subtrahendVectors[i]);

            handledGenesCount = genomeSize - genomeSize % Vector<double>.Count;
        }

        for (int i = handledGenesCount; i < genomeSize; i++)
            destination[i] = baseVector[i] + mutationForce * (minuend[i] - subtrahend[i]);
    }

    /// <summary>
    /// Computes <c>destination += mutationForce * (minuend - subtrahend)</c> in place.
    /// </summary>
    public static void AddScaledDifference(
        Span<double> destination,
        ReadOnlySpan<double> minuend,
        ReadOnlySpan<double> subtrahend,
        double mutationForce)
    {
        var genomeSize = destination.Length;
        var handledGenesCount = 0;

        if (Vector<double>.Count <= genomeSize)
        {
            var destinationVectors = MemoryMarshal.Cast<double, Vector<double>>(destination);
            var minuendVectors = MemoryMarshal.Cast<double, Vector<double>>(minuend);
            var subtrahendVectors = MemoryMarshal.Cast<double, Vector<double>>(subtrahend);

            for (int i = 0; i < destinationVectors.Length; i++)
                destinationVectors[i] += mutationForce * (minuendVectors[i] - subtrahendVectors[i]);

            handledGenesCount = genomeSize - genomeSize % Vector<double>.Count;
        }

        for (int i = handledGenesCount; i < genomeSize; i++)
            destination[i] += mutationForce * (minuend[i] - subtrahend[i]);
    }

    /// <summary>
    /// Computes the "current-to-target" base vector
    /// <c>destination = current + mutationForce * (target - current)</c>,
    /// used by current-to-best and current-to-pbest strategies.
    /// </summary>
    public static void AssignCurrentToTarget(
        Span<double> destination,
        ReadOnlySpan<double> current,
        ReadOnlySpan<double> target,
        double mutationForce)
    {
        AssignBasePlusScaledDifference(destination, current, target, current, mutationForce);
    }
}
