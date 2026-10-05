using DotNetDifferentialEvolution.GPU.Objectives;
using DotNetDifferentialEvolution.SelectionStrategies;
using DotNetDifferentialEvolution.SelectionStrategies.Interfaces;
using DotNetDifferentialEvolution.Variants;

namespace DotNetDifferentialEvolution.GPU.Test.Kernels;

/// <summary>
/// ACCEPTANCE.md, check S5, the configurations' side: each of the nine configurations accepts or refuses ties as the
/// CPU package's selection for it does (the variant's <see cref="DeVariantSetup.SelectionStrategy"/>, or the CPU
/// builder's default <see cref="SelectionStrategy"/> for the fixed schemes). On a flat objective every trial ties its
/// parent: a configuration that accepts ties replaces the population in generation 2, one that refuses keeps it.
/// </summary>
[Trait("Category", "Integration")]
public class TieRuleTests
{
    private const int PopulationSize = 8;
    private const long Budget = 1000;

    private static readonly double[] Lower = [-1.0, -1.0];
    private static readonly double[] Upper = [1.0, 1.0];

    /// <summary>The nine configurations, by the scheme stage method.</summary>
    /// <returns>The method names.</returns>
    public static TheoryData<string> Configurations() =>
    [
        nameof(IGpuMutationStrategyRequired<>.WithDefaultMutationStrategy),
        nameof(IGpuMutationStrategyRequired<>.WithBestMutationStrategy),
        nameof(IGpuMutationStrategyRequired<>.WithCurrentToBestMutationStrategy),
        nameof(IGpuMutationStrategyRequired<>.WithRandTwoMutationStrategy),
        nameof(IGpuMutationStrategyRequired<>.WithBestTwoMutationStrategy),
        nameof(IGpuMutationStrategyRequired<>.WithJde),
        nameof(IGpuMutationStrategyRequired<>.WithJade),
        nameof(IGpuMutationStrategyRequired<>.WithShade),
        nameof(IGpuMutationStrategyRequired<>.WithLShade),
    ];

    /// <summary>S5: the configuration replaces tied parents exactly when the CPU package's selection for it accepts a tie.</summary>
    /// <param name="configuration">The scheme stage method.</param>
    /// <returns>The run.</returns>
    [Theory]
    [MemberData(nameof(Configurations))]
    public async Task EachConfigurationTreatsTiesAsTheCpuPackageDoes(string configuration)
    {
        var cpuAcceptsTies = AcceptsATie(CpuSelection(configuration));

        var observer = new Generations();
        var stage = GpuDifferentialEvolutionBuilder.ForFunction(default(Flat)).WithBounds(Lower, Upper).WithPopulationSize(PopulationSize);
        using var optimizer = Scheme(stage, configuration)
            .WithGenerationLimit(2)
            .OnDevice(GpuDevice.Cpu)
            .WithSeed(1)
            .WithPopulationUpdateHandler(observer)
            .Build();
        _ = await optimizer.RunAsync().ConfigureAwait(true);

        Assert.Equal(2, observer.Genes.Count);
        var replaced = !observer.Genes[0].SequenceEqual(observer.Genes[1]);
        Assert.True(
            cpuAcceptsTies == replaced,
            $"{configuration}: the CPU package {(cpuAcceptsTies ? "accepts" : "refuses")} a tie, the GPU run {(replaced ? "replaced" : "kept")} the parents");
    }

    private static ISelectionStrategy CpuSelection(string configuration)
    {
        var setup = new DeVariantConfiguration(PopulationSize, Lower.Length, Lower, Upper);
        return configuration switch
        {
            nameof(IGpuMutationStrategyRequired<>.WithJde) => new JdeVariant().Configure(setup).SelectionStrategy!,
            nameof(IGpuMutationStrategyRequired<>.WithJade) => new JadeVariant().Configure(setup).SelectionStrategy!,
            nameof(IGpuMutationStrategyRequired<>.WithShade) => new ShadeVariant().Configure(setup).SelectionStrategy!,
            nameof(IGpuMutationStrategyRequired<>.WithLShade) => new LShadeVariant(Budget).Configure(setup).SelectionStrategy!,
            _ => new SelectionStrategy(Lower.Length),
        };
    }

    private static bool AcceptsATie(ISelectionStrategy selection) =>
        selection.SelectSurvivor(
            individualIndex: 0,
            trialIndividualFfValue: 1.0,
            trialIndividual: new double[Lower.Length],
            populationFfValues: [1.0],
            population: new double[Lower.Length],
            nextPopulationFfValues: new double[1],
            nextPopulation: new double[Lower.Length]) != SelectionOutcome.ParentKept;

    private static IGpuTerminationConditionRequired<Flat> Scheme(IGpuMutationStrategyRequired<Flat> stage, string configuration) => configuration switch
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

    /// <summary>The same value everywhere, so that every trial ties its parent.</summary>
    internal readonly struct Flat : IGpuFitnessFunction
    {
        /// <inheritdoc />
        public double Evaluate(GeneView genes) => 1.0;
    }

    /// <summary>The genes of each generation the observer sees.</summary>
    private sealed class Generations : IGpuPopulationUpdatedHandler
    {
        public List<double[]> Genes { get; } = [];

        public void Handle(GpuPopulationSnapshot snapshot)
        {
            ArgumentNullException.ThrowIfNull(snapshot);
            Genes.Add(snapshot.Genes.ToArray());
        }
    }
}
