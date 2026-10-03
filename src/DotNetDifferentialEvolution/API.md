# API.md — DotNetDifferentialEvolution (CPU package)

Namespace: `DotNetDifferentialEvolution`. The CPU package's two entry points: a staged
builder and the optimizer it builds. The consumer guide with worked examples is the
repository's `README.md` and `docs/AGENT_GUIDE.md`. Everything not listed here or in a
child's `API.md` is internal structure and may change.

## Builder ✅

```csharp
public class DifferentialEvolutionBuilder : IBoundsRequired, IPopulationSizeRequired,
    IPopulationSamplingRequired, IMutationStrategyRequired, ISelectionStrategyRequired,
    ITerminationConditionRequired, IWorkersCountRequired, IDifferentialEvolutionBuilder
{
    public static IBoundsRequired ForFunction(IFitnessFunctionEvaluator fitnessFunctionEvaluator);
}

public interface IBoundsRequired
{ IPopulationSizeRequired WithBounds(ReadOnlyMemory<double> lowerBound, ReadOnlyMemory<double> upperBound); }

public interface IPopulationSizeRequired
{ IPopulationSamplingRequired WithPopulationSize(int populationSize); }

public interface IPopulationSamplingRequired
{
    IMutationStrategyRequired WithPopulationSampling(IPopulationSamplingMaker populationSamplingMaker);
    IMutationStrategyRequired WithUniformPopulationSampling();
}

public interface IMutationStrategyRequired
{
    // A scheme, then a selection stage:
    ISelectionStrategyRequired WithMutationStrategy(IMutationStrategy mutationStrategy);
    ISelectionStrategyRequired WithMutationStrategy(IMutationStrategy mutationStrategy,
        IControlParameterProvider controlParameterProvider);
    ISelectionStrategyRequired WithDefaultMutationStrategy(double mutationForce, double crossoverProbability);
    ISelectionStrategyRequired WithBestMutationStrategy(double mutationForce, double crossoverProbability);
    ISelectionStrategyRequired WithCurrentToBestMutationStrategy(double mutationForce, double crossoverProbability);
    ISelectionStrategyRequired WithRandTwoMutationStrategy(double mutationForce, double crossoverProbability);
    ISelectionStrategyRequired WithBestTwoMutationStrategy(double mutationForce, double crossoverProbability);
    // Or a whole variant, which brings its own selection:
    ITerminationConditionRequired WithVariant(IDeVariant variant);
    ITerminationConditionRequired WithJde(double initialMutationForce = 0.5, double initialCrossoverProbability = 0.9);
    ITerminationConditionRequired WithJade(double pBestRate = 0.1, double archiveSizeRate = 1.0, double adaptationRate = 0.1);
    ITerminationConditionRequired WithShade(double pBestRate = 0.2, double archiveSizeRate = 1.0, int memorySize = 100);
    ITerminationConditionRequired WithLShade(long maxEvaluationNumber, double pBestRate = 0.11,
        double archiveSizeRate = 2.6, int memorySize = 6);
}

public interface ISelectionStrategyRequired
{
    ITerminationConditionRequired WithSelectionStrategy(ISelectionStrategy selectionStrategy);
    ITerminationConditionRequired WithDefaultSelectionStrategy();
}

public interface ITerminationConditionRequired
{ IWorkersCountRequired WithTerminationCondition(ITerminationStrategy terminationStrategy); }

public interface IWorkersCountRequired
{
    IDifferentialEvolutionBuilder UseProcessors(int processorsCount);
    IDifferentialEvolutionBuilder UseAllProcessors();          // Environment.ProcessorCount
}

public interface IDifferentialEvolutionBuilder
{
    IDifferentialEvolutionBuilder WithPopulationUpdateHandler(IPopulationUpdatedHandler populationUpdatedHandler);
    IDifferentialEvolutionBuilder WithLocalSearch(ILocalSearchRefiner refiner, int everyNGenerations = 1);
    IDifferentialEvolutionBuilder WithSeed(int seed);
    DifferentialEvolution Build();
}
```

The objective is **minimized**. Its worker overload `Evaluate(workerIndex, genes)` is
called concurrently from every worker; it must be pure or keep per-worker state indexed
by `workerIndex`. `Build` samples and evaluates the initial population (single-threaded,
`Evaluate(genes)`), so it costs `N` evaluations. `WithDefaultMutationStrategy` installs
the legacy `MutationStrategy`, which carries its own F and CR; the other four fixed-F
schemes get a `ConstantControlParameterProvider`.

## Optimizer ✅

```csharp
public class DifferentialEvolution : IDisposable
{
    public DifferentialEvolution(ProblemContext problemContext, IAlgorithmExecutor algorithmExecutor);
    public Task<Population> RunAsync();
    public Task<Population> RunAsync(CancellationToken cancellationToken = default);
    public void Dispose();
}
```

`RunAsync` starts the workers and returns the task of the final `Population` (read it
through its cursor, [Models](Models/API.md)). After the run has finished, calling it
again returns the same completed task; calling it while the run is in progress throws.
A token already canceled completes the task as canceled before any generation;
cancellation mid-run is observed at the generation barrier and completes the task as
canceled. `Dispose` stops every worker and waits until each has left its loop.

The public constructor is for hand-wired engines (tests, custom executors); the builder
is the supported path.

## Errors

| Situation | Behaviour |
|---|---|
| Bounds of different lengths, or lower > upper | `ArgumentException` from `WithBounds` |
| `populationSize <= 0`, `processorsCount <= 0` | `ArgumentException` |
| `everyNGenerations < 1` | `ArgumentOutOfRangeException` |
| `null` strategy, provider, sampler, variant, refiner | `ArgumentNullException` |
| Population below the mutation strategy's minimum | `InvalidOperationException` from `Build` |
| Strategy reads F and CR, no provider configured | `InvalidOperationException` from `Build` |
| A variant's `Validate` rejects the configuration | `InvalidOperationException` from `Build` |
| `RunAsync` while the run is in progress | `InvalidOperationException` |
| The objective, a hook, the observer or the stop rule throws during a run | the task faults with an `AggregateException` of the worker exceptions ([handlers](Controllers/WorkerControllerEventHandlers/API.md)) |

## Side effects

`Build` evaluates the objective `N` times; `RunAsync` starts `W` dedicated threads,
stopped by `Dispose`.

## Children

- [AlgorithmExecutors](AlgorithmExecutors/API.md) — one generation's work over a
  worker's stripe; [contract](AlgorithmExecutors/Interfaces/API.md).
- [Algorithms/Common](Algorithms/Common/API.md) — the adaptive variants' archive and
  ranking base.
- [Algorithms/Jde](Algorithms/Jde/API.md), [Jade](Algorithms/Jade/API.md),
  [Shade](Algorithms/Shade/API.md), [Lshade](Algorithms/Lshade/API.md) — the four
  parameter adaptations.
- [ControlParameterProviders](ControlParameterProviders/API.md) — the source of F and
  CR per trial; constant and dithered providers.
- [Controllers](Controllers/API.md) — the worker threads and the generation barrier;
  [handlers](Controllers/WorkerControllerEventHandlers/API.md) and
  [their contracts](Controllers/WorkerControllerEventHandlers/Interfaces/API.md).
- [GenerationStrategies](GenerationStrategies/API.md) — the between-generations hook of
  the adaptive variants and its narrowed context.
- [Helpers](Helpers/API.md) — the fitness ranking rule (`NaN` worst) and the population
  sort.
- [Interfaces](Interfaces/API.md) — the initial-population and observer hooks.
- [LocalSearch](LocalSearch/API.md) — the memetic refinement hook.
- [Models](Models/API.md) — `ProblemContext`, `PopulationView`, `TrialRecord`, and the
  result `Population` with its cursor ([cursor contracts](Models/Interfaces/API.md)).
- [MutationStrategies](MutationStrategies/API.md) — the seven DE schemes and
  `MutationContext`; [contract](MutationStrategies/Interfaces/API.md),
  [shared arithmetic](MutationStrategies/Helpers/API.md).
- [PopulationSamplingMaker](PopulationSamplingMaker/API.md) — uniform initial sampling.
- [RandomProviders](RandomProviders/API.md) — the per-worker xoshiro256** generator and
  the Gaussian and Cauchy samplers.
- [SelectionStrategies](SelectionStrategies/API.md) — greedy selection with separate
  survival and success thresholds, and its
  [contract](SelectionStrategies/Interfaces/API.md).
- [TerminationStrategies](TerminationStrategies/API.md) — generation, evaluation and
  stagnation limits, and their [contract](TerminationStrategies/Interfaces/API.md).
- [Variants](Variants/API.md) — `IDeVariant` and the jDE, JADE, SHADE and L-SHADE
  bundles.
