# API.md — GPU MutationStrategies

Namespace: `DotNetDifferentialEvolution.GPU.MutationStrategies`. The package's mutation
and crossover: DE/rand/1 with binomial crossover, constant F and CR. Everything not
listed here is internal structure and may change.

## DE/rand/1/bin ✅

```csharp
public readonly struct MutationStrategy<TRandomGenerator> : IMutationStrategy<TRandomGenerator>
    where TRandomGenerator : struct, IRandomGenerator
{
    public MutationStrategy(
        ArrayView<double> lowerBound,
        ArrayView<double> upperBound,
        double mutationForce = 0.3,
        double crossoverFactor = 0.8);

    public void Mutate(
        int index,
        DevicePopulation currentPopulation,
        DevicePopulation trialPopulation,
        TRandomGenerator random);
}
```

Implements [the contract](Interfaces/API.md). Note the parameter order: **lower bound
first**, the reverse of the sampler's constructor. The bounds are device views of length
`VectorSize`, allocated by the caller. For thread `index` with population size `N`:

1. Three donor indices `r0, r1, r2` are drawn, distinct from each other and from
   `index`: each is `random.Next(index) % (N - 1)`, shifted up by one if `>= index`; a
   draw equal to an earlier one is redrawn.
2. For every gene `j`: if `random.NextDouble(index) <= crossoverFactor`, the gene is
   `x[r0][j] + mutationForce * (x[r1][j] - x[r2][j])`; if that falls outside
   `[lower[j], upper[j]]`, it is replaced by a uniform draw in the box. Otherwise the
   gene is the parent's, `x[index][j]`.

There is no guaranteed mutant gene (`jrand`): with probability `(1 - CR)^D` the trial
equals its parent.

## Errors

| Situation | Behaviour |
|---|---|
| `N <= 3` | Not detected. With `N` 2 or 3 the donor loop cannot find three distinct indices and never ends; with `N` 1 it takes a remainder by zero. Either hangs or corrupts the kernel |
| Bounds shorter than `VectorSize` | Not detected; reads outside the bound buffers |
| `mutationForce`, `crossoverFactor` out of their usual ranges | Accepted as given |

## Side effects

Writes the genes of trial `index`; advances random stream `index` by
`3 + D + (genes out of the box)` draws, more when donor draws collide.

## Out of scope

- Other schemes (best/1, current-to-best, rand/2), parameter dithering or adaptation.
- Allocating or freeing the bound buffers.
