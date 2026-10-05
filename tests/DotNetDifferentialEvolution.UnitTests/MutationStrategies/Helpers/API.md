# API.md — UnitTests/MutationStrategies/Helpers

Nothing outward. What this node proves about the arithmetic every mutation scheme
shares.

## What this node guarantees

| Claim | Confirmed by | State |
|---|---|---|
| Binomial crossover takes `jrand` from the mutant always and other genes when the draw is at most CR | `CrossoverHelperTests`, exact cases | ✅ |
| An out-of-bounds gene is repaired to the midpoint between the bound and the parent's gene, with no draw | `CrossoverHelperTests`, exact cases | ✅ |
| The mutant-gene rate is `CR + (1 − CR)/D`, and `jrand` is uniform over the genome | `CrossoverHelperTests`, statistical cases | ✅ |
| The SIMD vector operations equal the scalar formula on both sides of the vector width | `MutationMathTests` | ✅ |
| Distinct-index selection skips the excluded index and retries collisions | `RandomIndexSelectorTests` | ✅ |

## Tests ✅

```csharp
[Trait("Category", "Unit")]
public class CrossoverHelperTests
{
    public void MixesMutantAndParentGenesAndRepairsOutOfBounds();
    public void GuaranteedGeneAlwaysComesFromMutantEvenWhenCrossoverNeverFires();
    public void InBoundsMutantGenesAreKeptWhenCrossoverAlwaysFires();
    public void RepairReflectsOutOfBoundGenesHalfwayTowardTheParent();
    public void GeneInheritanceRateMatchesTheClosedForm(double crossoverProbability, int genomeSize);
    public void TheGuaranteedGeneIsUniformlyDistributedOverTheGenome();
}
[Trait("Category", "Unit")]
public class MutationMathTests
{
    public static IEnumerable<object[]> GenomeSizes();
    public void AssignBasePlusScaledDifferenceMatchesScalarReference(int genomeSize);
    public void AddScaledDifferenceAccumulatesOntoDestination(int genomeSize);
    public void AssignCurrentToTargetMovesCurrentTowardTarget(int genomeSize);
    public void AssignCurrentToTargetWithForceZeroYieldsCurrent();
    public void AssignCurrentToTargetWithForceOneYieldsTarget();
}
[Trait("Category", "Unit")]
public class RandomIndexSelectorTests
{
    public void ShiftsCandidatesPastExcludedIndex();
    public void RetriesUntilCandidateIsDistinct();
    public void ProducesDistinctInRangeIndicesNeverEqualToExcluded(int populationSize, int excludeIndex, int count);
}
```
