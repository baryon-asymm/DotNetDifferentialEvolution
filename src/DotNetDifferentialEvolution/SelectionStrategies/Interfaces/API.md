# API.md — selection contract

Namespace: `DotNetDifferentialEvolution.SelectionStrategies.Interfaces`. The one-to-one
survival rule of the CPU engine. Everything not listed here is internal structure and
may change.

## Contract ✅

```csharp
public interface ISelectionStrategy
{
    SelectionOutcome SelectSurvivor(
        int individualIndex,
        double trialIndividualFfValue,
        Span<double> trialIndividual,
        Span<double> populationFfValues,
        Span<double> population,
        Span<double> nextPopulationFfValues,
        Span<double> nextPopulation);
}
```

For individual `individualIndex`: decide between the parent (in `population`,
`populationFfValues`) and the evaluated trial (`trialIndividual`,
`trialIndividualFfValue`), write the winner's genes and fitness into the next
population at that index, and return what happened as a
[`SelectionOutcome`](../API.md). The populations are flat, row-major:
individual `i` is the slice `[i * genomeSize, (i + 1) * genomeSize)`.

The return value must describe what was written: `ParentKept` if the parent was
written; `TrialAccepted` or `TrialImproved` if the trial was. The engine feeds it to the
archive and to parameter adaptation, which key on `TrialImproved`, and to jDE, which
keys on any replacement.

## Errors

Implementation-defined; the contract names none.

## Side effects

Writes one individual and one fitness value of the next population.

## Out of scope

- Population-wide selection: a call sees one index.
