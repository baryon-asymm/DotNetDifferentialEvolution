# API.md — DotNetDifferentialEvolution.IntegrationTests

The node exposes nothing outward: nobody references a test project. Its contract
points upward: it is what the CPU package may consider proven as a whole.

## What this node guarantees

| Claim | Confirmed by | State |
|---|---|---|
| The executor's loop, alone on one thread and seeded, reaches the optimum of Sphere 5-D and Rosenbrock 2-D (1e-6 in value, 1e-3 per gene) | I0, `AlgorithmExecutorConvergenceTests` | ✅ |
| A single worker and a master with slaves converge and stop; an objective's exception faults the result with `AggregateException` and stops every worker | I1, `WorkerControllerTests`, `WorkersOrchestratorTests` | ✅ |
| Start/stop on a worker follows its state machine over 2 000 seeded commands | I1, `WorkerControllerTests` (`Slow`) | ✅ local only |
| `NaN` never becomes the reported best: per-worker scan, cross-worker reduction, population scan, the builder's initial pick, a JADE run | I1, `NaNFitnessTests` | ✅ |
| The engine keeps the fitness ranking current whenever the mutation strategy declares it, with or without a generation strategy | I1, `FitnessRankingMaintenanceTests` | ✅ |
| The trial record says what the selection strategy decided, including a strategy that is wrong about its ties | I1, `TrialOutcomeReportingTests` | ✅ |
| Cancellation, reproducibility, oversubscription, no leaks | I2, [Concurrency](Concurrency/API.md) | ✅ |
| Every scheme and variant converges; benchmarks; local search; live population size; the guide's examples | I3, [EndToEnd](EndToEnd/API.md) | ✅ |

## Children

[Concurrency](Concurrency/API.md), [EndToEnd](EndToEnd/API.md),
[TestSupport](TestSupport/API.md).

## Tests ✅

```csharp
[Trait("Category", "Integration")]
public class AlgorithmExecutorConvergenceTests
{
    public void ConvergesOnSphere();
    public void ConvergesOnRosenbrock();
}
[Trait("Category", "Integration")]
public class WorkerControllerTests
{
    public async Task ConvergesAndStopsOnTermination();
    public async Task PropagatesFitnessFunctionExceptionAndStops();
    public async Task RandomStopAndStartKeepsConsistentStateThenTerminates();   // Slow
}
[Trait("Category", "Integration")]
public class WorkersOrchestratorTests
{
    public async Task AllWorkersCooperateToConverge();
    public async Task FitnessFunctionExceptionPropagatesFromAnyWorkerAndStopsAll();
}
[Trait("Category", "Integration")]
public class NaNFitnessTests
{
    public async Task PerWorkerScan_DoesNotReportANaNIndividualAsTheBest();
    public async Task CrossWorkerReduction_DoesNotReportANaNIndividualAsTheBest();
    public async Task PopulationScan_DoesNotReportANaNIndividualAsTheBest();
    public async Task PopulationScan_WithAnAllNaNPopulation_StillReportsAnInRangeIndex();
    public async Task Builder_DoesNotHandANaNIndividualToMutationAsTheInitialBest();
    public async Task JadeRun_WithANaNInTheInitialPopulation_ReportsAFiniteBest();
}
[Trait("Category", "Integration")]
public class FitnessRankingMaintenanceTests
{
    public async Task AHandWiredCurrentToPBestRunKeepsTheRankingCurrent();
    public async Task AThirdPartyGenerationStrategyDoesNotHaveToMaintainTheRankingItself();
    public async Task TheAdaptiveVariantsStillRankCorrectly(string variant);
    public async Task AHandWiredCurrentToPBestRunConvergesLikeTheAdaptiveOnes();
}
[Trait("Category", "Integration")]
public class TrialOutcomeReportingTests
{
    public async Task TheBuiltInStrategyTakesATieWithoutCreditingItAsAnImprovement();
    public async Task ARejectEverythingSelectionStrategyIsReportedAsKeepingTheParent();
    public async Task AStrategyThatCallsATieAnImprovementIsReportedAsItClaims();
}
```
