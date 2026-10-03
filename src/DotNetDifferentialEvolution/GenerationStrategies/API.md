# API.md — GenerationStrategies

Namespace: `DotNetDifferentialEvolution.GenerationStrategies`. The between-generations
hook of the adaptive variants and the narrowed view of the run it gets. Everything not
listed here is internal structure and may change.

## Hook ✅

```csharp
public interface IGenerationStrategy
{
    void AfterGeneration(GenerationContext context, ReadOnlySpan<TrialRecord> trialRecords);
    void UseRandomProvider(BaseRandomProvider randomProvider) { }   // default: ignore
}
```

Called once per generation, single-threaded on the orchestrator thread, after every
trial has been evaluated and the populations have been swapped. Only the first
`context.ActivePopulationSize` records are meaningful. `UseRandomProvider` is called at
most once, by the builder, for a seeded run; the provider is used by nothing else.

## Narrowed context ✅

```csharp
public sealed class GenerationContext
{
    public GenerationContext(ProblemContext context);

    public PopulationView CurrentPopulation { get; }      // the generation just produced
    public PopulationView DiscardedParents { get; }       // parents just replaced; scratch
    public int ActivePopulationSize { get; set; }         // narrows both views
    public long EvaluationCount { get; }                  // read-only to the hook
    public Memory<double> Archive { get; }
    public int ArchiveSize { get; set; }
    public int ArchiveCapacity { get; set; }
    public Memory<int> FitnessSortedIndices { get; }
    public MutationRequirements MutationRequirements { get; }
    public int GenomeSize { get; }
}
```

What a published variant needs between generations and nothing more: no swap, no best
index, no termination strategy, no evaluator. `CurrentPopulation`'s buffers are writable
(L-SHADE compacts survivors through them). `FitnessSortedIndices` is already fresh when
the mutation strategy declared `FitnessRanking`.

## Errors

| Situation | Behaviour |
|---|---|
| `new GenerationContext(null)` | `ArgumentNullException` |

## Side effects

A hook may write the current population, the archive, its size and capacity, and the
live population size.

## Out of scope

- Parameter sampling per trial: [ControlParameterProviders](../ControlParameterProviders/API.md).
