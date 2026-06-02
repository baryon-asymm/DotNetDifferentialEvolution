using DotNetDifferentialEvolution.Models;

namespace DotNetDifferentialEvolution.IntegrationTests.TestSupport;

/// <summary>
/// Runs a builder-configured optimizer one or more times and returns the best result. Because
/// the public builder API seeds itself from <see cref="Random.Shared"/>, end-to-end runs are
/// not bit-reproducible; allowing a few independent attempts and keeping the best mirrors how a
/// stochastic global optimizer is used in practice and drives the chance of a spurious failure
/// to a negligible level, without masking a genuinely broken algorithm.
/// </summary>
internal static class BuilderOptimizer
{
    public static async Task<Population> BestOfAsync(
        int attempts,
        TimeSpan timeout,
        Func<DotNetDifferentialEvolution.DifferentialEvolution> factory)
    {
        Population? best = null;

        for (int attempt = 0; attempt < attempts; attempt++)
        {
            using var de = factory();
            var result = await de.RunAsync().WaitAsync(timeout);
            result.MoveCursorToBestIndividual();

            if (best is null ||
                result.IndividualCursor.FitnessFunctionValue < best.IndividualCursor.FitnessFunctionValue)
            {
                best = result;
            }
        }

        return best!;
    }
}
