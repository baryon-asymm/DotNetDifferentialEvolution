# Using this library: a task-oriented guide

Written for a coding agent that has the package and needs working code on the first attempt, and
for anyone who would rather read a checklist than prose. It states the contracts, the constraints,
and the mistakes that compile and run but produce a wrong search.

For the mathematics — every variant in its paper's notation, cited to the equation, with the type
that implements it — see [ALGORITHMS.md](ALGORITHMS.md) next to this file.

Both documents ship inside the NuGet package, so a restored copy is on disk under
`<packages>/dotnetdifferentialevolution/<version>/docs/` and is pinned to the version in use.

---

## What this library does

- Minimizes a real-valued function of a **fixed-length vector of doubles** over a **box** (per-gene
  lower and upper bounds). **Lower is better.** To maximize, return the negated value.
- Runs the population in parallel across dedicated threads, one stripe of the population per worker.
- Ships classic DE plus four self-adaptive variants: jDE, JADE, SHADE, L-SHADE.

## What it does not do

- **No constraints beyond the box.** No equality, inequality, or penalty machinery. Encode those in
  the objective yourself.
- **No integer, categorical, or variable-length genomes.** Genome size is fixed at `WithBounds`.
- **No async objective.** `Evaluate` is synchronous and CPU-bound by design; calling into I/O from
  it blocks a worker thread for the whole run. Asynchronous and GPU evaluation are the separate
  `DotNetDifferentialEvolution.GPU` package's problem, not this one's.
- **No multi-objective optimization.** One scalar per individual.

---

## 1. The shortest complete program

```csharp
using DotNetDifferentialEvolution;
using DotNetDifferentialEvolution.TerminationStrategies;
using DotNetOptimization.Abstractions;

public sealed class Sphere : IFitnessFunctionEvaluator
{
    public double Evaluate(ReadOnlySpan<double> genes)
    {
        var sum = 0.0;
        foreach (var gene in genes)
            sum += gene * gene;

        return sum;
    }

    public double Evaluate(int workerIndex, ReadOnlySpan<double> genes) => Evaluate(genes);
}

using var de = DifferentialEvolutionBuilder
    .ForFunction(new Sphere())
    .WithBounds(new[] { -5.0, -5.0, -5.0 }, new[] { 5.0, 5.0, 5.0 })
    .WithPopulationSize(50)
    .WithUniformPopulationSampling()
    .WithDefaultMutationStrategy(mutationForce: 0.5, crossoverProbability: 0.9)
    .WithDefaultSelectionStrategy()
    .WithTerminationCondition(new LimitGenerationNumberTerminationStrategy(200))
    .UseAllProcessors()
    .Build();

var population = await de.RunAsync();

population.MoveCursorToBestIndividual();
var best = population.IndividualCursor.GetSnapshot(deepCopy: true);

Console.WriteLine($"f = {best.FitnessFunctionValue}, x = [{string.Join(", ", best.Genes.ToArray())}]");
```

The builder is **staged**: each call returns the interface holding only the calls that are legal
next. You cannot reorder or omit a required step — that is a compile error, not a run-time one, so
follow the chain and let completion guide you.

## 2. The objective contract

`IFitnessFunctionEvaluator` (from `DotNetOptimization.Abstractions`) has two members. Implement both.

```csharp
double Evaluate(ReadOnlySpan<double> genes);
double Evaluate(int workerIndex, ReadOnlySpan<double> genes);
```

- **The overload with `workerIndex` is the one the engine calls.** It is invoked **concurrently**
  from every worker thread. Either make the evaluation pure (then delegate to the single-argument
  form, as above), or index per-worker scratch buffers by `workerIndex`, which is stable and in
  `[0, workerCount)`. Sharing one mutable buffer across workers is a data race.
- **`genes` is valid only for the duration of the call.** It points into the live population. Do not
  store the span or anything derived from it by reference.
- **Returning `NaN` is safe.** NaN is ranked worse than every real value, so a NaN individual is
  replaceable and can never become the incumbent best. It is the correct return for "this point is
  infeasible"; returning `double.MaxValue` also works but distorts nothing less.
- The engine never calls `Evaluate` with an out-of-box vector: genes are repaired before evaluation.

## 3. Choosing a variant

| Call | Algorithm | Use when | Carries |
|---|---|---|---|
| `WithDefaultMutationStrategy(F, CR)` | `DE/rand/1/bin` | You want a baseline, or you will tune F and CR yourself | needs `WithDefaultSelectionStrategy()` after it |
| `WithJde()` | jDE (2006) | You want self-adaptation with no budget planning | N ≥ 4 |
| `WithJade()` | JADE (2009) | Moderate budgets; the archive helps on multimodal problems | N ≥ 4 |
| `WithShade()` | SHADE (2013) | A stronger default than JADE, still budget-agnostic | N ≥ 4 |
| `WithLShade(budget)` | L-SHADE (2014) | You know the evaluation budget; CEC-2014 competition winner | N ≥ 4, **budget must match termination** |

**The four presets install the selection strategy themselves.** After `WithJde/WithJade/WithShade/
WithLShade` the builder moves straight to the termination stage — there is no selection call to
make, and the type system will not offer one.

**Default when unsure:** `WithLShade` if the run is budgeted by evaluations, `WithShade` otherwise.

### L-SHADE has one hard requirement

Its population shrinks linearly toward 4 as the budget is consumed, so the budget it plans against
and the budget that actually stops the run must be the same number:

```csharp
const long Budget = 300_000;
const int Dimensions = 30;

using var de = DifferentialEvolutionBuilder
    .ForFunction(objective)
    .WithBounds(lowerBound, upperBound)
    .WithPopulationSize(18 * Dimensions)   // r_N^init = 18 from the paper's Table II
    .WithUniformPopulationSampling()
    .WithLShade(maxEvaluationNumber: Budget)
    .WithTerminationCondition(new LimitEvaluationNumberTerminationStrategy(Budget))
    .UseAllProcessors()
    .Build();
```

Mismatch the two numbers and `Build()` throws with both values named. Pair L-SHADE with a
*generation* or *stagnation* limit instead and **nothing complains** — see §6.

## 4. What the compiler checks for you

Everything about **shape**: that bounds, population size, sampling, a mutation strategy or variant,
a selection strategy (unless a variant supplied one), a termination condition and a worker count are
all present, in that order. A configuration that omits a step does not exist as a type.

## 5. What `Build()` checks

Cross-cutting facts no type can express. Each message names the remedy; read it rather than
guessing:

- population size against the mutation strategy's minimum;
- a mutation strategy that reads per-individual F and CR but was given no control-parameter provider;
- L-SHADE's budget against the termination budget;
- bounds of equal length, with `lower <= upper` elementwise;
- population size and worker count positive.

## 6. What nothing checks — the real pitfalls

1. **L-SHADE with a non-evaluation termination strategy.** The budget check only fires against
   `LimitEvaluationNumberTerminationStrategy`. With a generation limit the run either ends with the
   population still far above its floor, or spends its tail collapsed at 4 individuals. Silent.
2. **A seed does not survive a change of worker count.** Individual *i* draws from worker
   *i mod W*'s stream, so `WithSeed(42).UseProcessors(4)` and `WithSeed(42).UseProcessors(8)` are
   different runs. Both reproducible, neither portable. `UseAllProcessors()` makes the run
   machine-dependent by construction — pin `UseProcessors(n)` if a seeded run must travel.
3. **A seed is reproducible only within a minor version.** Changing how the engine consumes
   randomness reshuffles every seeded run without being a defect.
4. **`IndividualCursor` is a cursor over live memory, not a value.** Move the cursor and everything
   you read from it changes. Anything that must outlive the call needs
   `GetSnapshot(deepCopy: true)`; the default `deepCopy: false` keeps referencing the population's
   own gene array.
5. **`DifferentialEvolution` is one-shot and `IDisposable`.** A second `RunAsync` returns the
   already-completed task rather than starting a new search. To run again, build again. Dispose it,
   or the worker threads outlive the run — `using var` is the correct default.
6. **`WithShade` is SHADE 1.0** (CEC 2013), not the SHADE 1.1 the authors distribute as source. The
   two differ in the memory update. `WithLShade` is built on 1.1, as the L-SHADE paper specifies.
7. **A tie is resolved per variant.** SHADE and L-SHADE take a trial equal to its parent, JADE keeps
   the parent. Both follow their own paper; do not "fix" one to match the other.
8. **`IPopulationUpdatedHandler.Handle` runs on a worker thread inside the generation barrier.**
   Cheap bookkeeping only. Blocking there stalls the whole population.

## 7. Reading the result

`RunAsync` returns the final `Population`. It is a cursor-based view, not a list:

```csharp
population.MoveCursorToBestIndividual();
var best = population.IndividualCursor.GetSnapshot(deepCopy: true);

for (var i = 0; i < population.PopulationSize; i++)
{
    population.MoveCursorTo(i);
    // population.IndividualCursor is now individual i
}
```

Use `population.PopulationSize`, never `Capacity`: under L-SHADE the backing arrays stay at the
initial size while the live population shrinks, so the slots past `PopulationSize` are no longer
part of the search.
`GenerationNumber` and `EvaluationCount` report what the run actually spent.

## 8. Cancellation

```csharp
var population = await de.RunAsync(cancellationToken);
```

Observed at the next generation barrier, so a run with an expensive objective stops within roughly
one generation rather than instantly, with the population in a consistent state. The task completes
as canceled (`OperationCanceledException`).

## 9. Extending it

Implement the interface, pass it to the matching builder call:

| Interface | Replaces | Given to |
|---|---|---|
| `IFitnessFunctionEvaluator` | the objective | `ForFunction` |
| `ITerminationStrategy` | when to stop | `WithTerminationCondition` |
| `IPopulationSamplingMaker` | the initial population | `WithPopulationSampling` |
| `IMutationStrategy` | the trial-vector operator | `WithMutationStrategy` |
| `ISelectionStrategy` | survival | `WithSelectionStrategy` |
| `IPopulationUpdatedHandler` | per-generation observation | `WithPopulationUpdateHandler` |
| `ILocalSearchRefiner` | memetic polishing between generations | `WithLocalSearch` |
| `IDeVariant` | all of the above as one consistent bundle | `WithVariant` |

Write an `IDeVariant` rather than assembling parts by hand when the pieces depend on each other: an
adaptive scheme is meaningless without the operator that reads the parameters it adapts, and a
variant is validated as a unit.

## 10. Where the rest is

- [ALGORITHMS.md](ALGORITHMS.md) (next to this file) — the formulas, the citations, the
  implementing types, and every deliberate deviation from the papers.
- The package ships XML documentation, so hovering any member gives its contract, and SourceLink,
  so a debugger steps into the library's own sources.
- `README.md` — narrative introduction and the full builder surface.
