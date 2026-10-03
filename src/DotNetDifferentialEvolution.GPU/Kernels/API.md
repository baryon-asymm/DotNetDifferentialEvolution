# API.md — DotNetDifferentialEvolution.GPU/Kernels

Namespace: `DotNetDifferentialEvolution.GPU.Kernels`. Internal to the assembly; visible
to the GPU test project and to ILGPU's runtime assembly. Consumed by the package root.

## Internal to the assembly ✅

```csharp
internal readonly record struct StepParameters(int Seed, int Generation,
    int PopulationSize, int GenomeSize, double MutationForce, ulong CrossoverThreshold);

internal readonly record struct PopulationViews(ArrayView<double> Current,
    ArrayView<double> CurrentFitness, ArrayView<double> Next, ArrayView<double> NextFitness,
    ArrayView<double> Trial, ArrayView<double> LowerBound, ArrayView<double> UpperBound)
{
    public PopulationViews Swapped();
}

internal static class DeStep
{
    public static ulong CrossoverThreshold(double crossoverProbability);
    public static void PickDonors<TDraws>(ref TDraws draws, int individual,
        int populationSize, out int r1, out int r2, out int r3)
        where TDraws : struct, IDrawSource;
    public static void BuildTrial<TDraws>(ref TDraws draws, int individual,
        StepParameters parameters, ArrayView<double> population, ArrayView<double> trial,
        ArrayView<double> lowerBound, ArrayView<double> upperBound)
        where TDraws : struct, IDrawSource;
    public static bool Survives(double trialFitness, double parentFitness);
}

internal static class GpuKernels
{
    public static void Initialize<TFunction>(Index1D index, TFunction function,
        StepParameters parameters, PopulationViews views)
        where TFunction : struct, IGpuFitnessFunction;
    public static void Generation<TFunction>(Index1D index, TFunction function,
        StepParameters parameters, PopulationViews views)
        where TFunction : struct, IGpuFitnessFunction;
    public static void DrawSequence(Index1D index, StepParameters parameters,
        int individual, ArrayView<uint> output);
    public static void PhiloxBlocks(Index1D index, ArrayView<uint> counters,
        ArrayView<uint> keys, ArrayView<uint> blocks);
}
```

- `CrossoverThreshold(CR)` is the CPU package's `RandomThreshold.Scale`: `CR·2⁶⁴`, 0 at
  or below 0, `ulong.MaxValue` at or above 1. A gene crosses when a 64-bit draw is at
  most the threshold.
- `PickDonors`: each of r1, r2, r3 is a draw from the `N − 1` indices other than i,
  redrawn while it repeats an earlier one.
- `BuildTrial`: `v = x_r1 + F·(x_r2 − x_r3)` into slot i of `trial`; then `jrand` is
  drawn, and gene `jrand` and every gene whose 64-bit draw passes the threshold keep the
  mutant gene, repaired to `(bound + parent)/2` if it left the box; every other gene is
  the parent's. No draw is consumed for gene `jrand`, as in the CPU step.
- `Initialize`: thread i samples `lower + u·(upper − lower)` per gene from its
  generation-0 draws, then evaluates. `Generation`: thread i builds its trial, evaluates
  it and writes the survivor and its fitness into slot i of `Next`.
- `DrawSequence` and `PhiloxBlocks` exist for the cross-backend checks 3a and 4b.
