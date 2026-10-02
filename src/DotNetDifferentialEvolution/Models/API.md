# API.md — Models

Namespace: `DotNetDifferentialEvolution.Models`. The run's state and what a consumer
receives: the problem context the engine runs on, the population views over its flat
buffers, the per-trial record, and the result population with its cursor. Everything not
listed here is internal structure and may change.

## Result population and cursor ✅

```csharp
public class Population : IIndividualCursorUpdater
{
    public Population(ReadOnlyMemory<double> genes, ReadOnlyMemory<double> fitnessFunctionValues);

    public IndividualCursor IndividualCursor { get; init; }
    public int GenerationNumber { get; set; }
    public int GenomeSize { get; }            // genes.Length / Capacity
    public int PopulationSize { get; internal set; }   // live individuals
    public int Capacity { get; }              // allocated individuals
    public int BestIndividualIndex { get; set; }
    public long EvaluationCount { get; set; }

    public void MoveCursorTo(int individualIndex);
    public void MoveCursorToBestIndividual();
    public void Update(int individualIndex, ref double fitnessFunctionValue,
        ref ReadOnlyMemory<double> genes);
}

public class IndividualCursor : IIndividualCursor, ISolution
{
    public IndividualCursor(double fitnessFunctionValue, ReadOnlyMemory<double> genes);
    public double FitnessFunctionValue { get; }
    public ReadOnlyMemory<double> Genes { get; }
    public void AcceptUpdater(int individualIndex, IIndividualCursorUpdater updater);
    public IndividualCursor GetSnapshot(bool deepCopy = false);
}
```

`Population` is what `RunAsync` returns and what observers and stop rules receive. Its
cursor is one reusable object: `MoveCursorTo(i)` repoints it at individual `i` of the
**live** population (`0 .. PopulationSize - 1`) without copying. `GetSnapshot()` freezes
the cursor's current target; with `deepCopy: true` the genes are copied, otherwise they
still alias the population's buffer. `IndividualCursor` is an `ISolution`, so it can seed
another optimizer of the family. Before the first move the cursor holds `double.MaxValue`
and individual 0's genes.

## Population view ✅

```csharp
public readonly record struct PopulationView(
    Memory<double> Genes, Memory<double> FfValues, int Count, int GenomeSize)
{
    public int Capacity { get; }
    public Span<double> GenesOf(int individualIndex);
    public Span<double> ActiveGenes { get; }
    public Span<double> ActiveFfValues { get; }
}
```

A population as the engine and the hooks see it: flat row-major genes (individual `i`
is `[i * GenomeSize, (i + 1) * GenomeSize)`), fitness values, the live count and the
allocated capacity, which differ once a strategy shrinks the population.

## Problem context ✅

```csharp
public class ProblemContext
{
    public ProblemContext(int populationSize, int genomeSize, int workersCount,
        ReadOnlyMemory<double> genesLowerBound, ReadOnlyMemory<double> genesUpperBound,
        IFitnessFunctionEvaluator fitnessFunctionEvaluator,
        ITerminationStrategy terminationStrategy,
        Memory<double> population, Memory<double> populationFfValues,
        Memory<double> trialPopulation, Memory<double> trialPopulationFfValues);

    // configuration, init-only
    public int PopulationSize { get; init; }
    public int GenomeSize { get; init; }
    public int WorkersCount { get; init; }
    public ReadOnlyMemory<double> GenesLowerBound { get; init; }
    public ReadOnlyMemory<double> GenesUpperBound { get; init; }
    public IFitnessFunctionEvaluator FitnessFunctionEvaluator { get; init; }
    public ITerminationStrategy TerminationStrategy { get; init; }
    public IPopulationUpdatedHandler? PopulationUpdatedHandler { get; init; }
    public IControlParameterProvider? ControlParameterProvider { get; init; }
    public IGenerationStrategy? GenerationStrategy { get; init; }
    public int? RandomSeed { get; init; }
    public MutationRequirements MutationRequirements { get; init; }
    public ILocalSearchRefiner? LocalSearchRefiner { get; init; }
    public int LocalSearchInterval { get; init; }   // default 1

    // run state
    public Memory<TrialRecord> TrialRecords { get; }
    public int CurrentPopulationSize { get; set; }
    public int BestIndividualIndex { get; set; }
    public long EvaluationCount { get; set; }
    public Memory<double> Archive { get; set; }
    public int ArchiveSize { get; set; }
    public int ArchiveCapacity { get; set; }
    public Memory<int> FitnessSortedIndices { get; }
    public PopulationView CurrentPopulation { get; }
    public PopulationView TrialPopulation { get; }

    public void SwapPopulations();
    public Population GetRepresentativePopulation(int generationNumber, int bestIndividualIndex);
}
```

The whole run in one object; the builder creates it, the engine and the local-search
hook work on it. `CurrentPopulationSize`'s setter narrows **both** views at once.
`SwapPopulations()` exchanges the current and trial views (and the two `Population`
objects behind them). `GetRepresentativePopulation` stamps the generation, best index,
evaluation count and live size onto the current `Population` and returns it — the same
object each call for a given buffer.

## Trial record ✅

```csharp
public record struct TrialRecord
{
    public SelectionOutcome Outcome { get; set; }
    public readonly bool Replaced { get; }   // Outcome != ParentKept
    public readonly bool Improved { get; }   // Outcome == TrialImproved
    public double UsedF { get; set; }
    public double UsedCr { get; set; }
    public double ParentFfValue { get; set; }
    public double TrialFfValue { get; set; }
}
```

One per individual per generation; workers write disjoint indices, the generation
strategy reads them. A zeroed record reads as `ParentKept`.

## Errors

| Situation | Behaviour |
|---|---|
| `MoveCursorTo` outside `0 .. PopulationSize - 1` | `ArgumentOutOfRangeException` |
| `AcceptUpdater(…, null)` | `ArgumentNullException` |
| `ProblemContext` with inconsistent buffer lengths | Not checked |

## Side effects

`MoveCursorTo` repoints the shared cursor: every holder of `population.IndividualCursor`
sees the move.

## Out of scope

- Building a context: `DifferentialEvolutionBuilder` does it and validates it.
