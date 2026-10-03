# API.md — TerminationStrategies

Namespace: `DotNetDifferentialEvolution.TerminationStrategies`. The built-in stop rules:
generation limit, evaluation budget, stagnation streak. Everything not listed here is
internal structure and may change. The contract is in [Interfaces](Interfaces/API.md).

## Generation limit ✅

```csharp
public class LimitGenerationNumberTerminationStrategy : ITerminationStrategy
{
    public LimitGenerationNumberTerminationStrategy(int maxGenerationNumber);
    public int MaxGenerationNumber { get; init; }
    public bool ShouldTerminate(Population population);
}
```

`true` when `population.GenerationNumber >= MaxGenerationNumber`.

## Evaluation budget ✅

```csharp
public class LimitEvaluationNumberTerminationStrategy : ITerminationStrategy
{
    public LimitEvaluationNumberTerminationStrategy(long maxEvaluationNumber);
    public long MaxEvaluationNumber { get; init; }
    public bool ShouldTerminate(Population population);
}
```

`true` when `population.EvaluationCount >= MaxEvaluationNumber`. The budget L-SHADE's
schedule is tied to; the builder checks that the two agree.

## Stagnation streak ✅

```csharp
public class StagnationStreakTerminationStrategy : ITerminationStrategy
{
    public StagnationStreakTerminationStrategy(int maxStagnationStreak, double stagnationThreshold);
    public int MaxStagnationStreak { get; init; }
    public double StagnationThreshold { get; init; }
    public int CurrentStagnationStreak { get; }
    public double LastBestFitnessFunctionValue { get; }   // starts at double.MinValue
    public bool ShouldTerminate(Population population);
}
```

Moves the population's cursor to the best individual and compares its fitness with the
last recorded best: a change larger than `StagnationThreshold` (in either direction)
records the new best and resets the streak, otherwise the streak grows. `true` when the
streak reaches `MaxStagnationStreak`. **Stateful**: one instance per run.

## Errors

| Situation | Behaviour |
|---|---|
| `null` population | `ArgumentNullException` |
| Negative or zero limits, negative threshold | Accepted as given |

## Side effects

The stagnation rule moves `population.IndividualCursor` and updates its own counters.

## Out of scope

- Fitness targets, wall time, combinations of rules.
