# API.md — UnitTests/Models

Nothing outward. What this node proves about the population, its view, its cursor and
the problem context.

## What this node guarantees

| Claim | Confirmed by | State |
|---|---|---|
| A snapshot copies the value; a deep one isolates the genes, a shallow one shares them | `IndividualCursorTests` | ✅ |
| `Population` derives sizes from its buffers; the genome size stays tied to capacity when the population shrinks; the cursor refuses indices outside the live population | `PopulationTests` | ✅ |
| `PopulationView` separates capacity from the live count; narrowing the context narrows both views and survives a swap | `PopulationViewTests` | ✅ |
| `ProblemContext` sizes its records and ranking, swaps buffers, and stamps the representative population | `ProblemContextTests` | ✅ |

## Tests ✅

```csharp
[Trait("Category", "Unit")]
public class IndividualCursorTests
{
    public void Snapshot_PreservesValueAndGenes();
    public void ShallowSnapshot_SharesGeneStorage();
    public void DeepSnapshot_CopiesGeneStorage();
}
[Trait("Category", "Unit")]
public class PopulationTests
{
    public void DerivesPopulationAndGenomeSizeFromBuffers();
    public void MoveCursorTo_PointsCursorAtTheRequestedIndividual();
    public void MoveCursorToBestIndividual_UsesBestIndividualIndex();
    public void APopulationStartsFullyActive();
    public void GenomeSizeStaysDerivedFromTheCapacityWhenThePopulationShrinks();
    public void MoveCursorTo_RefusesAnIndexOutsideTheActivePopulation(int individualIndex);
}
[Trait("Category", "Unit")]
public class PopulationViewTests
{
    public void CapacityIsTheAllocatedLengthAndCountIsTheLiveOne();
    public void GenesOfSlicesTheIndividualOutOfTheArena();
    public void TheActiveSpansStopAtCountRatherThanAtCapacity();
    public void NarrowingTheContextNarrowsBothViewsAtOnce();
    public void SwappingKeepsBothViewsNarrowed();
    public void NarrowingLeavesTheBuffersAllocatedAtFullCapacity();
}
[Trait("Category", "Unit")]
public class ProblemContextTests
{
    public void Constructor_InitializesDerivedState();
    public void SwapPopulations_ExchangesCurrentAndTrialBuffers();
    public void GetRepresentativePopulation_StampsGenerationBestAndEvaluationCount();
}
```
