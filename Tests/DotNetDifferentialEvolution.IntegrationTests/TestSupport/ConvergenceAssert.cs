using DotNetDifferentialEvolution.Models;
using DotNetDifferentialEvolution.Tests.Shared.FitnessFunctionEvaluators.Interfaces;

namespace DotNetDifferentialEvolution.IntegrationTests.TestSupport;

/// <summary>
/// Assertion helpers for "did the optimizer actually reach the known optimum" checks shared
/// by the convergence and benchmark tests.
/// </summary>
internal static class ConvergenceAssert
{
    /// <summary>
    /// Asserts the population's best fitness is within <paramref name="valueTolerance"/> of the
    /// function's known global minimum, and (optionally) that the genes are within
    /// <paramref name="geneTolerance"/> of a known minimizer.
    /// </summary>
    public static void ReachedOptimum(
        ITestFitnessFunctionEvaluator evaluator,
        Population population,
        double valueTolerance,
        double? geneTolerance = null)
    {
        var expectedValue = evaluator.GetGlobalMinimumFfValue();
        var actualValue = population.IndividualCursor.FitnessFunctionValue;

        Assert.True(
            Math.Abs(actualValue - expectedValue) <= valueTolerance,
            $"Expected fitness ~{expectedValue} (±{valueTolerance}) but got {actualValue}.");

        if (geneTolerance is null)
            return;

        var expectedGenes = evaluator.GetGlobalMinimumGenes();
        var actualGenes = population.IndividualCursor.Genes;
        for (int i = 0; i < expectedGenes.Length; i++)
            Assert.Equal(expectedGenes.Span[i], actualGenes.Span[i], geneTolerance.Value);
    }
}
