using System.Buffers.Binary;
using System.Security.Cryptography;

namespace DotNetDifferentialEvolution.GPU.Test.EndToEnd;

/// <summary>
/// Check P0 of the Kernels node's ACCEPTANCE.md: the single-kernel path is unchanged by the refactoring that lets the
/// pointwise kernels share its selection and sampling. Each of the nine configurations of check S13 (Sphere D = 10 in
/// [−5, 5], N = 100, L-SHADE N_init 180, seed 1) runs 2·10⁴ evaluations on the CPU accelerator, and one SHA-256 is taken over,
/// configuration by configuration in S13's order, the best genes and the fitness as IEEE-754 bits, then the generation and
/// the evaluation counts. The expected hash was measured at the base commit, before any change to the kernels.
/// </summary>
[Trait("Category", "Integration")]
public class SingleKernelPathTests
{
    private const string ExpectedHash = "CF28D8AEF44E3C1AA1620D0D303B37958BA35A875A9B9C5BEF7A04BFBD7ECF91";
    private const int GenomeSize = 10;
    private const long Budget = 20_000;

    private static readonly double[] Lower = [.. Enumerable.Repeat(-5.0, GenomeSize)];
    private static readonly double[] Upper = [.. Enumerable.Repeat(5.0, GenomeSize)];

    /// <summary>P0: the nine runs hash to the value measured at the base commit.</summary>
    /// <returns>The case.</returns>
    [Fact]
    public async Task TheNineRunsHashToTheValueMeasuredBeforeTheRefactoring()
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        foreach (var configuration in SymmetryRunTests.Configurations().Cast<object[]>().Select(row => (string)row[0]))
        {
            using var optimizer = Build(configuration);
            var result = await optimizer.RunAsync().ConfigureAwait(true);
            foreach (var gene in result.Genes.ToArray())
            {
                Append(hash, BitConverter.DoubleToInt64Bits(gene));
            }

            Append(hash, BitConverter.DoubleToInt64Bits(result.FitnessFunctionValue));
            Append(hash, result.Generations);
            Append(hash, result.EvaluationCount);
        }

        Assert.Equal(ExpectedHash, Convert.ToHexString(hash.GetHashAndReset()));
    }

    private static void Append(IncrementalHash hash, long value)
    {
        Span<byte> bytes = stackalloc byte[sizeof(long)];
        BinaryPrimitives.WriteInt64LittleEndian(bytes, value);
        hash.AppendData(bytes);
    }

    private static GpuDifferentialEvolution Build(string configuration)
    {
        var stage = GpuDifferentialEvolutionBuilder
            .ForFunction(default(Sphere))
            .WithBounds(Lower, Upper)
            .WithPopulationSize(configuration == nameof(IGpuMutationStrategyRequired<>.WithLShade) ? 180 : 100);
        return Scheme(stage, configuration).WithEvaluationLimit(Budget).OnDevice(GpuDevice.Cpu).WithSeed(1).Build();
    }

    private static IGpuTerminationConditionRequired<Sphere> Scheme(IGpuMutationStrategyRequired<Sphere> stage, string configuration) => configuration switch
    {
        nameof(IGpuMutationStrategyRequired<>.WithDefaultMutationStrategy) => stage.WithDefaultMutationStrategy(0.5, 0.9),
        nameof(IGpuMutationStrategyRequired<>.WithBestMutationStrategy) => stage.WithBestMutationStrategy(0.5, 0.9),
        nameof(IGpuMutationStrategyRequired<>.WithCurrentToBestMutationStrategy) => stage.WithCurrentToBestMutationStrategy(0.5, 0.9),
        nameof(IGpuMutationStrategyRequired<>.WithRandTwoMutationStrategy) => stage.WithRandTwoMutationStrategy(0.5, 0.9),
        nameof(IGpuMutationStrategyRequired<>.WithBestTwoMutationStrategy) => stage.WithBestTwoMutationStrategy(0.5, 0.9),
        nameof(IGpuMutationStrategyRequired<>.WithJde) => stage.WithJde(),
        nameof(IGpuMutationStrategyRequired<>.WithJade) => stage.WithJade(),
        nameof(IGpuMutationStrategyRequired<>.WithShade) => stage.WithShade(),
        nameof(IGpuMutationStrategyRequired<>.WithLShade) => stage.WithLShade(Budget),
        _ => throw new ArgumentOutOfRangeException(nameof(configuration), configuration, "Not a configuration."),
    };
}
