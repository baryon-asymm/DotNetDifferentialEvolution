using DotNetDifferentialEvolution.Models;

namespace DotNetDifferentialEvolution.IntegrationTests.TestSupport;

/// <summary>
/// Runs a builder-configured optimizer once and returns its population with the cursor on the
/// best individual. Callers seed the builder (<see cref="IDifferentialEvolutionBuilder.WithSeed"/>),
/// so a convergence test is one reproducible run: it fails the same way every time, and a variant
/// that converges only sometimes cannot pass by being given several tries.
/// </summary>
/// <remarks>
/// This replaced a best-of-3/4 over unseeded runs, written before the builder could be seeded.
/// A seed is reproducible only within a minor version of the engine; a change to how it consumes
/// randomness reshuffles every seeded run, and a test that then fails says the variant does not
/// converge on that run, which is a finding, not a reason to try other seeds.
/// </remarks>
internal static class BuilderOptimizer
{
    /// <summary>The seed every end-to-end convergence test uses.</summary>
    public const int Seed = 1;

    public static async Task<Population> RunOnceAsync(
        TimeSpan timeout,
        Func<DotNetDifferentialEvolution.DifferentialEvolution> factory)
    {
        ArgumentNullException.ThrowIfNull(factory);

        using var de = factory();
        var result = await de.RunAsync().WaitAsync(timeout);
        result.MoveCursorToBestIndividual();

        return result;
    }
}
