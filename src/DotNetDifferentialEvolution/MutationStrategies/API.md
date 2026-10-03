# API.md — MutationStrategies

Namespace: `DotNetDifferentialEvolution.MutationStrategies`. The built-in DE schemes and
the context every scheme builds a trial from. Everything not listed here is internal
structure and may change. The contract is in [Interfaces](Interfaces/API.md); the shared
arithmetic in [Helpers](Helpers/API.md).

## Context ✅

```csharp
public readonly ref struct MutationContext
{
    public int IndividualIndex { get; init; }
    public int BestIndividualIndex { get; init; }
    public int PopulationSize { get; init; }          // live individuals
    public int GenomeSize { get; init; }
    public double MutationForce { get; init; }        // NaN without a provider
    public double CrossoverProbability { get; init; } // NaN without a provider
    public ReadOnlySpan<double> Population { get; init; }
    public ReadOnlySpan<double> PopulationFfValues { get; init; }
    public Span<double> TrialIndividual { get; init; }
    public ReadOnlySpan<double> LowerBound { get; init; }
    public ReadOnlySpan<double> UpperBound { get; init; }
    public BaseRandomProvider RandomProvider { get; init; }
    internal SeededRandomProvider? WorkerRandomProvider { get; init; }
    public ReadOnlySpan<double> Archive { get; init; }
    public int ArchiveSize { get; init; }
    public ReadOnlySpan<int> FitnessSortedIndices { get; init; }
}
```

Built by the engine on the worker for each trial. `RandomProvider` and
`WorkerRandomProvider` are the same object (the latter typed concretely so the helpers can
inline the draw; `null` when the context was not built by the engine).

## Strategies ✅

```csharp
public class MutationStrategy : IMutationStrategy                  // DE/rand/1/bin, own F and CR
{
    public const int NumberOfIndividualsToChoose = 3;
    [Obsolete] public MutationStrategy(double mutationForce, double crossoverProbability,
        int populationSize, ReadOnlyMemory<double> lowerBound,
        ReadOnlyMemory<double> upperBound, BaseRandomProvider randomProvider);
    public MutationStrategy(double mutationForce, double crossoverProbability,
        int populationSize, ReadOnlyMemory<double> lowerBound,
        ReadOnlyMemory<double> upperBound);
    public int MinimumPopulationSize { get; }                      // 4
    public MutationRequirements Requirements { get; }              // None
    public void Mutate(in MutationContext context);
}

public class RandMutationStrategy : IMutationStrategy { }          // DE/rand/1/bin, F/CR from context
public class RandTwoMutationStrategy : IMutationStrategy { }       // DE/rand/2/bin
public class BestMutationStrategy : IMutationStrategy { }          // DE/best/1/bin
public class BestTwoMutationStrategy : IMutationStrategy { }       // DE/best/2/bin
public class CurrentToBestMutationStrategy : IMutationStrategy { } // DE/current-to-best/1/bin

public class CurrentToPBestMutationStrategy : IMutationStrategy   // DE/current-to-pbest/1 (JADE)
{
    public CurrentToPBestMutationStrategy(double pBestRate);
    public CurrentToPBestMutationStrategy(double pBestRateMin, double pBestRateMax);
}
```

The five shown with empty bodies have only the implicit parameterless constructor; like
the other two they implement `MinimumPopulationSize`, `Requirements` and `Mutate`.

| Strategy | Mutant `v` | Min. N | Requirements |
|---|---|---|---|
| `MutationStrategy` | `x_r1 + F (x_r2 - x_r3)`, F and CR from the constructor | 4 | `None` |
| `RandMutationStrategy` | `x_r1 + F (x_r2 - x_r3)` | 4 | `ControlParameters` |
| `RandTwoMutationStrategy` | `x_r1 + F (x_r2 - x_r3) + F (x_r4 - x_r5)` | 6 | `ControlParameters` |
| `BestMutationStrategy` | `x_best + F (x_r1 - x_r2)` | 3 | `ControlParameters`, `BestIndividual` |
| `BestTwoMutationStrategy` | `x_best + F (x_r1 - x_r2) + F (x_r3 - x_r4)` | 5 | `ControlParameters`, `BestIndividual` |
| `CurrentToBestMutationStrategy` | `x_i + F (x_best - x_i) + F (x_r1 - x_r2)` | 3 | `ControlParameters`, `BestIndividual` |
| `CurrentToPBestMutationStrategy` | `x_i + F (x_pbest - x_i) + F (x_r1 - x_r2)`, `x_r2` from population ∪ archive | 4 | `ControlParameters`, `FitnessRanking`, `Archive` |

The `r` indices are distinct and differ from `i`. For p-best, the rate is drawn per trial
from `[min, max]`; the pool is the top `round(p N)` (half away from zero) of the ranking,
floored at `min(2, N)`; without a ranking `x_pbest` is drawn from the whole population.
Every strategy ends with binomial crossover (`jrand`) and halfway repair
([Helpers](Helpers/API.md)).

## Errors

| Situation | Behaviour |
|---|---|
| p-best rate outside `(0, 1]`, or min > max | `ArgumentOutOfRangeException` / `ArgumentException`, in the constructor |
| Population below `MinimumPopulationSize` | Refused by the builder; called directly, the index draw never ends |

## Side effects

Write the trial buffer; draw from the worker's provider.

## Out of scope

- F/CR adaptation: [ControlParameterProviders](../ControlParameterProviders/API.md) and
  the variants.
