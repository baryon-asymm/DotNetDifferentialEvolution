using System.Reflection;
using DotNetDifferentialEvolution.AlgorithmExecutors;
using DotNetDifferentialEvolution.Algorithms.Jade;
using DotNetDifferentialEvolution.Algorithms.Jde;
using DotNetDifferentialEvolution.Algorithms.Lshade;
using DotNetDifferentialEvolution.Algorithms.Shade;
using DotNetDifferentialEvolution.ControlParameterProviders;
using DotNetDifferentialEvolution.Controllers;
using DotNetDifferentialEvolution.GenerationStrategies;
using DotNetDifferentialEvolution.Models;
using DotNetDifferentialEvolution.MutationStrategies;
using DotNetDifferentialEvolution.MutationStrategies.Interfaces;
using DotNetDifferentialEvolution.SelectionStrategies;
using DotNetDifferentialEvolution.SelectionStrategies.Interfaces;
using DotNetDifferentialEvolution.TerminationStrategies;
using DotNetDifferentialEvolution.TerminationStrategies.Interfaces;
using DotNetDifferentialEvolution.Tests.Shared.FitnessFunctionEvaluators;
using DotNetDifferentialEvolution.Variants;

namespace DotNetDifferentialEvolution.UnitTests.Builder;

/// <summary>
/// A DE variant is a bundle of decisions — which mutation operator, where F and CR come from,
/// what happens between generations, how large the archive is — that has to be installed together
/// or not at all. These tests pin the four presets' assembled configuration so that expressing
/// them through the extension point cannot quietly change what they build, and they check that a
/// variant defined outside the library is held to the same integrity rules as a built-in one.
/// </summary>
[Trait("Category", "Unit")]
public class DeVariantTests
{
    private static SphereEvaluator Evaluator => new(dimension: 2);

    [Fact]
    public void JdeInstallsRandOneWithASingleObjectAsProviderAndGenerationStrategy()
    {
        using var de = BuildPreset(builder => builder.WithJde());

        var context = ContextOf(de);

        Assert.IsType<RandMutationStrategy>(MutationStrategyOf(de));
        Assert.IsType<SelectionStrategy>(SelectionStrategyOf(de));
        Assert.IsType<JdeStrategy>(context.ControlParameterProvider);
        Assert.Same(context.ControlParameterProvider, context.GenerationStrategy);
        Assert.Equal(0, context.ArchiveCapacity);
    }

    [Fact]
    public void JadeInstallsCurrentToPBestWithAnArchiveSizedFromThePopulation()
    {
        using var de = BuildPreset(builder => builder.WithJade(archiveSizeRate: 1.0));

        var context = ContextOf(de);

        Assert.IsType<CurrentToPBestMutationStrategy>(MutationStrategyOf(de));
        Assert.IsType<SelectionStrategy>(SelectionStrategyOf(de));
        Assert.IsType<JadeStrategy>(context.ControlParameterProvider);
        Assert.Same(context.ControlParameterProvider, context.GenerationStrategy);
        Assert.Equal(PopulationSize, context.ArchiveCapacity);
    }

    [Fact]
    public void ShadeInstallsCurrentToPBestBackedByTheSuccessHistoryMemory()
    {
        using var de = BuildPreset(builder => builder.WithShade(archiveSizeRate: 1.0));

        var context = ContextOf(de);

        Assert.IsType<CurrentToPBestMutationStrategy>(MutationStrategyOf(de));
        Assert.IsType<ShadeStrategy>(context.ControlParameterProvider);
        Assert.Same(context.ControlParameterProvider, context.GenerationStrategy);
        Assert.Equal(PopulationSize, context.ArchiveCapacity);
    }

    [Fact]
    public void LShadeInstallsCurrentToPBestWithTheLargerArchiveItsPaperSpecifies()
    {
        using var de = BuildPreset(
            builder => builder.WithLShade(maxEvaluationNumber: EvaluationBudget, archiveSizeRate: 2.6),
            new LimitEvaluationNumberTerminationStrategy(EvaluationBudget));

        var context = ContextOf(de);

        Assert.IsType<CurrentToPBestMutationStrategy>(MutationStrategyOf(de));
        Assert.IsType<LShadeStrategy>(context.ControlParameterProvider);
        Assert.Same(context.ControlParameterProvider, context.GenerationStrategy);
        // round(2.6 * 20) = 52, rounded half away from zero.
        Assert.Equal(52, context.ArchiveCapacity);
    }

    [Theory]
    [InlineData("jde")]
    [InlineData("jade")]
    [InlineData("shade")]
    [InlineData("lshade")]
    public void EveryPresetSatisfiesItsOwnMutationStrategysRequirements(
        string preset)
    {
        using var de = preset == "lshade"
            ? BuildPreset(
                builder => builder.WithLShade(maxEvaluationNumber: EvaluationBudget),
                new LimitEvaluationNumberTerminationStrategy(EvaluationBudget))
            : BuildPreset(builder => preset switch
            {
                "jde" => builder.WithJde(),
                "jade" => builder.WithJade(),
                "shade" => builder.WithShade(),
                _ => throw new ArgumentOutOfRangeException(nameof(preset))
            });

        var context = ContextOf(de);
        var requirements = MutationStrategyOf(de).Requirements;

        Assert.Equal(requirements, context.MutationRequirements);
        if (requirements.HasFlag(MutationRequirements.ControlParameters))
            Assert.NotNull(context.ControlParameterProvider);
    }

    [Fact]
    public void AThirdPartyVariantIsConfiguredWithTheProblemDimensions()
    {
        var variant = new RecordingVariant();

        using var de = BuildPreset(builder => builder.WithVariant(variant));

        Assert.Equal(PopulationSize, variant.SeenConfiguration!.Value.PopulationSize);
        Assert.Equal(2, variant.SeenConfiguration.Value.GenomeSize);
        Assert.Equal(2, variant.SeenConfiguration.Value.LowerBound.Length);
    }

    [Fact]
    public void AThirdPartyVariantsValidateRunsAgainstTheCompletedConfiguration()
    {
        var variant = new RecordingVariant();

        using var de = BuildPreset(builder => builder.WithVariant(variant));

        Assert.NotNull(variant.SeenTerminationStrategy);
        Assert.IsType<LimitGenerationNumberTerminationStrategy>(variant.SeenTerminationStrategy);
    }

    [Fact]
    public void AThirdPartyVariantCanRejectTheConfigurationFromItsOwnValidate()
    {
        var exception = Assert.Throws<InvalidOperationException>(
            () => BuildPreset(builder => builder.WithVariant(new RejectingVariant())));

        Assert.Contains("this variant refuses", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AThirdPartyVariantGetsTheSameControlParameterCheckAsABuiltIn()
    {
        // It installs a strategy that reads F and CR but forgets the provider — the same mistake
        // the single-argument WithMutationStrategy overload used to let through silently.
        var exception = Assert.Throws<InvalidOperationException>(
            () => BuildPreset(builder => builder.WithVariant(
                new StubVariant(new RandMutationStrategy(), controlParameterProvider: null))));

        Assert.Contains("control", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AThirdPartyVariantGetsTheSameMinimumPopulationCheckAsABuiltIn()
    {
        // DE/rand/2 needs six individuals; the preset here configures twenty, so the variant has
        // to be checked against a population it does not itself choose.
        var evaluator = Evaluator;

        var exception = Assert.Throws<InvalidOperationException>(() =>
            DifferentialEvolutionBuilder.ForFunction(evaluator)
                .WithBounds(evaluator.GetLowerBounds(), evaluator.GetUpperBounds())
                .WithPopulationSize(5)
                .WithUniformPopulationSampling()
                .WithVariant(new StubVariant(
                    new RandTwoMutationStrategy(), new ConstantControlParameterProvider(0.5, 0.9)))
                .WithTerminationCondition(new LimitGenerationNumberTerminationStrategy(1))
                .UseProcessors(1)
                .Build());

        Assert.Contains("too small", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AVariantThatChoosesNoSelectionStrategyGetsTheGreedyDefault()
    {
        using var de = BuildPreset(builder => builder.WithVariant(new StubVariant(
            new RandMutationStrategy(), new ConstantControlParameterProvider(0.5, 0.9))));

        Assert.IsType<SelectionStrategy>(SelectionStrategyOf(de));
    }

    [Fact]
    public void WithVariantRejectsNull()
    {
        Assert.Throws<ArgumentNullException>(
            () => BuildPreset(builder => builder.WithVariant(null!)));
    }

    private const int PopulationSize = 20;
    private const long EvaluationBudget = 1000;

    private static DifferentialEvolution BuildPreset(
        Func<IMutationStrategyRequired, ITerminationConditionRequired> configure,
        ITerminationStrategy? terminationStrategy = null)
    {
        var evaluator = Evaluator;

        return configure(
                DifferentialEvolutionBuilder.ForFunction(evaluator)
                    .WithBounds(evaluator.GetLowerBounds(), evaluator.GetUpperBounds())
                    .WithPopulationSize(PopulationSize)
                    .WithUniformPopulationSampling())
            .WithTerminationCondition(terminationStrategy ?? new LimitGenerationNumberTerminationStrategy(1))
            .UseProcessors(1)
            .Build();
    }

    private static ProblemContext ContextOf(
        DifferentialEvolution differentialEvolution)
        => Assert.IsType<ProblemContext>(
            PrivateField(typeof(DifferentialEvolution), "_problemContext").GetValue(differentialEvolution));

    private static IMutationStrategy MutationStrategyOf(
        DifferentialEvolution differentialEvolution)
        => Assert.IsAssignableFrom<IMutationStrategy>(
            PrivateField(typeof(AlgorithmExecutor), "_mutationStrategy").GetValue(ExecutorOf(differentialEvolution)));

    private static ISelectionStrategy SelectionStrategyOf(
        DifferentialEvolution differentialEvolution)
        => Assert.IsAssignableFrom<ISelectionStrategy>(
            PrivateField(typeof(AlgorithmExecutor), "_selectionStrategy").GetValue(ExecutorOf(differentialEvolution)));

    /// <summary>
    /// Every worker shares one executor, so the first controller is as good as any. The engine's
    /// internals are deliberately not public; reading them reflectively is preferable to widening
    /// the API for a test.
    /// </summary>
    private static object ExecutorOf(
        DifferentialEvolution differentialEvolution)
    {
        var controllers = (Memory<WorkerController>)PrivateField(
            typeof(DifferentialEvolution), "_workerControllers").GetValue(differentialEvolution)!;

        return PrivateField(typeof(WorkerController), "_algorithmExecutor").GetValue(controllers.Span[0])!;
    }

    private static FieldInfo PrivateField(
        Type declaringType,
        string name)
        => declaringType.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)
           ?? throw new InvalidOperationException($"{declaringType.Name}.{name} was renamed; update this test.");

    /// <summary>A variant that installs whatever it is handed, so a test can install a mismatch.</summary>
    private sealed class StubVariant : IDeVariant
    {
        private readonly IMutationStrategy _mutationStrategy;
        private readonly IControlParameterProvider? _controlParameterProvider;
        private readonly IGenerationStrategy? _generationStrategy;

        public StubVariant(
            IMutationStrategy mutationStrategy,
            IControlParameterProvider? controlParameterProvider,
            IGenerationStrategy? generationStrategy = null)
        {
            _mutationStrategy = mutationStrategy;
            _controlParameterProvider = controlParameterProvider;
            _generationStrategy = generationStrategy;
        }

        public DeVariantSetup Configure(
            in DeVariantConfiguration configuration)
            => new()
            {
                MutationStrategy = _mutationStrategy,
                ControlParameterProvider = _controlParameterProvider,
                GenerationStrategy = _generationStrategy
            };
    }

    /// <summary>Records what the builder handed it, so the contract can be asserted on.</summary>
    private sealed class RecordingVariant : IDeVariant
    {
        public DeVariantConfiguration? SeenConfiguration { get; private set; }

        public ITerminationStrategy? SeenTerminationStrategy { get; private set; }

        public DeVariantSetup Configure(
            in DeVariantConfiguration configuration)
        {
            SeenConfiguration = configuration;

            return new DeVariantSetup
            {
                MutationStrategy = new RandMutationStrategy(),
                ControlParameterProvider = new ConstantControlParameterProvider(0.5, 0.9)
            };
        }

        public void Validate(
            in DeVariantConfiguration configuration,
            ITerminationStrategy terminationStrategy)
            => SeenTerminationStrategy = terminationStrategy;
    }

    /// <summary>A variant whose own cross-check fails, to prove the builder honours it.</summary>
    private sealed class RejectingVariant : IDeVariant
    {
        public DeVariantSetup Configure(
            in DeVariantConfiguration configuration)
            => new()
            {
                MutationStrategy = new RandMutationStrategy(),
                ControlParameterProvider = new ConstantControlParameterProvider(0.5, 0.9)
            };

        public void Validate(
            in DeVariantConfiguration configuration,
            ITerminationStrategy terminationStrategy)
            => throw new InvalidOperationException("this variant refuses the configuration");
    }
}
