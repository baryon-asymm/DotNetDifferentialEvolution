# API.md — IntegrationTests/EndToEnd

Nothing outward. What this node proves about the package used through its builder.

## What this node guarantees

| Claim | Confirmed by | State |
|---|---|---|
| jDE, JADE and SHADE reach Rosenbrock 2-D's optimum (1e-6, genes 1e-3); L-SHADE does on a 200 000-evaluation budget | `AdaptiveVariantsConvergenceTests` | ✅ (one run, seed 1) |
| Classic DE reaches 1e-6 on five unimodal functions; SHADE reaches 1e-4 on seven multimodal ones | `BenchmarkConvergenceTests` | ✅ (one run, seed 1) |
| L-SHADE reaches `1e-2·max(1, |f*|)` on Schwefel 2-D, Styblinski-Tang 2-D, Rastrigin 5-D, Ackley 5-D in 300 000 evaluations | `BenchmarkConvergenceTests` (`Slow`) | ✅ local only |
| best/1, current-to-best/1, rand/2, best/2 and dithered best/1 each reach 1e-3 on Sphere 5-D | `MutationStrategyConvergenceTests` | ✅ (one run, seed 1) |
| The guide's examples build and run: the shortest program, L-SHADE spending its budget down to 4 individuals, the cursor walk | `DocumentedExampleTests` | ✅ |
| Local search fires on its cadence, its write-back survives, its evaluations are counted | `LocalSearchHookTests` | ✅ |
| An L-SHADE observer sees the live size shrink monotonically to 4, only live individuals, a fixed capacity and genome size | `PopulationSizeReportingTests` | ✅ |
| An 8-D sphere feasible on `x₀ < -4` and `double.MaxValue` elsewhere (bounds ±5), population 100, 20 000 evaluations, seed 12345, one worker: SHADE and L-SHADE hand the objective no vector with a `NaN` gene, and the best individual has none | `SentinelFitnessTests` | ✅ (one run each) |

## Tests ✅

```csharp
[Trait("Category", "Integration")]
public class AdaptiveVariantsConvergenceTests
{
    public async Task SelfAdaptiveVariantsConvergeOnRosenbrock(string variant);
    public async Task LShadeConvergesOnRosenbrock();
}
[Trait("Category", "Integration")]
public class BenchmarkConvergenceTests
{
    public async Task ClassicDeConvergesOnUnimodalFunctions(string functionName, int dimension);
    public async Task ShadeConvergesOnMultimodalFunctions(string functionName, int dimension);
    public async Task LShadeConvergesOnHarderMultimodalFunctions(string functionName, int dimension);   // Slow
}
[Trait("Category", "Integration")]
public class MutationStrategyConvergenceTests
{
    public async Task EachStrategyConvergesOnSphere(string strategy);
}
[Trait("Category", "Integration")]
public class DocumentedExampleTests
{
    public async Task TheShortestCompleteProgramBuildsRunsAndReportsAMinimum();
    public async Task TheLShadeExampleBuildsAndSpendsItsBudget();
    public async Task TheResultIsReadThroughTheCursor();
}
[Trait("Category", "Integration")]
public class LocalSearchHookTests
{
    public async Task RefinerRunsOnConfiguredCadenceAndWriteBackSurvivesIntoResult();
    public async Task RefinerEvaluationsAreFoldedIntoEvaluationCount();
}
[Trait("Category", "Integration")]
public class PopulationSizeReportingTests
{
    public async Task AnLShadeRunReportsTheActivePopulationShrinkingToItsMinimum();
    public async Task TheReportedIndividualsAreAllLive();
    public async Task CapacityKeepsReportingTheAllocatedLength();
    public async Task TheGenomeSizeDoesNotDriftWithTheShrinkingPopulation();
}
[Trait("Category", "Integration")]
public class SentinelFitnessTests
{
    public async Task TheObjectiveIsNeverGivenANaNGeneWhenMostParentsScoreTheSentinel(string variant);   // "SHADE", "L-SHADE"
}
```
