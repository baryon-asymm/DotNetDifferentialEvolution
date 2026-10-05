# API.md — DotNetDifferentialEvolution.GPU/Kernels

Namespace: `DotNetDifferentialEvolution.GPU.Kernels`. Internal to the assembly; visible
to the GPU test project and to ILGPU's runtime assembly. Consumed by the package root.

## Internal to the assembly ✅

```csharp
internal readonly record struct StepParameters(int Seed, int Generation,
    int PopulationSize, int GenomeSize, double MutationForce, ulong CrossoverThreshold,
    SchemeKind Scheme = SchemeKind.RandOne, ParameterRule Rule = ParameterRule.Fixed,
    TieRule Ties = TieRule.Accepted, double PBestRateMin = 0.0, double PBestRateMax = 0.0,
    int MemorySize = 0);

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
    public static int DrawDistinct<TDraws>(ref TDraws draws, int individual, int populationSize,
        int taken0, int taken1, int taken2, int taken3)
        where TDraws : struct, IDrawSource;
    public static void CrossAndRepair<TDraws>(ref TDraws draws, int individual, int genomeSize,
        ulong crossoverThreshold, ArrayView<double> population, ArrayView<double> trial,
        ArrayView<double> lowerBound, ArrayView<double> upperBound)
        where TDraws : struct, IDrawSource;
    public static bool Survives(double trialFitness, double parentFitness);
}

internal static class GpuKernels
{
    public static void Initialize<TFunction>(Index1D index, TFunction function,
        StepParameters parameters, PopulationViews views)
        where TFunction : struct, IGpuFitnessFunction;
    public static void Generation<TFunction, TRule>(Index1D index, TFunction function,
        StepParameters parameters, PopulationViews views, StrategyViews strategy)
        where TFunction : struct, IGpuFitnessFunction
        where TRule : struct, IControlParameterRule;
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

## Symmetry ✅

Built 2026-10-05 (checks S2–S6 and S13 of the package's [ACCEPTANCE.md](../ACCEPTANCE.md)).

```csharp
internal enum SchemeKind { RandOne = 0, Best = 1, CurrentToBest = 2, RandTwo = 3, BestTwo = 4, CurrentToPBest = 5 }
internal enum ParameterRule { Fixed = 0, Jde = 1, Jade = 2, Shade = 3 }
internal enum TieRule { Accepted = 0, Refused = 1 }

internal readonly record struct StrategyViews(ArrayView<int> Stop, ArrayView<int> BestIndex,
    ArrayView<int> Ranking, ArrayView<double> Archive, ArrayView<int> ArchiveSize,
    ArrayView<double> Adaptation, ArrayView<double> MutationForces,
    ArrayView<double> CrossoverProbabilities, ArrayView<int> Outcomes);

internal static class Schemes                // each the CPU class's, draw for draw (S2, S3)
{
    public static int MinimumPopulationSize(SchemeKind scheme);
    public static void BuildTrial<TDraws>(ref TDraws draws, int individual,
        StepParameters parameters, double mutationForce, ulong crossoverThreshold,
        PopulationViews views, StrategyViews strategy)
        where TDraws : struct, IDrawSource;
    public static double RoundHalfAwayFromZero(double value);
}

internal static class ControlParameters      // the CPU strategies' samplers (S4)
{
    public static void Jde<TDraws>(ref TDraws draws, double currentMutationForce,
        double currentCrossoverProbability, out double mutationForce, out double crossoverProbability)
        where TDraws : struct, IDrawSource;
    public static void Jade<TDraws>(ref TDraws draws, double meanCrossoverProbability,
        double meanMutationForce, out double mutationForce, out double crossoverProbability)
        where TDraws : struct, IDrawSource;
    public static void Shade<TDraws>(ref TDraws draws, ArrayView<double> memory, int memorySize,
        out double mutationForce, out double crossoverProbability)
        where TDraws : struct, IDrawSource;
    public static double Gaussian<TDraws>(ref TDraws draws, double mean, double standardDeviation)
        where TDraws : struct, IDrawSource;
    public static double Cauchy<TDraws>(ref TDraws draws, double location, double scale)
        where TDraws : struct, IDrawSource;
    public static double ClampToUnit(double value);
}

internal interface IControlParameterRule     // FixedRule, JdeRule, JadeRule, ShadeRule
{
    void Draw<TDraws>(ref TDraws draws, int individual, StepParameters parameters,
        StrategyViews strategy, out double mutationForce, out double crossoverProbability)
        where TDraws : struct, IDrawSource;
}

internal static class Selection              // S5
{
    public const int Kept = 0;
    public const int Accepted = 1;
    public const int Improved = 2;
    public static int Outcome(double trialFitness, double parentFitness, bool acceptsTies);
    public static bool IsBetter(double candidate, double incumbent);
    public static bool IsBetterOrEqual(double candidate, double incumbent);
}
```

- `Generation` returns at once when the stop word is set. It draws F and CR by its rule
  type argument first, then builds the trial by the scheme, evaluates it and selects. N
  is the current population size (L-SHADE).
- The rule is a type argument so that the fixed schemes' and jDE's kernels reach no
  `Log`, `Cos` or `Tan`: they compile on a CUDA context built without libdevice.
- jDE: F_i and CR_i become the trial's where it replaced the parent, ties included (S6).
  JADE, SHADE, L-SHADE: thread i writes its F, CR and outcome for the bookkeeping.
- `Gaussian` is the CPU package's Box–Muller with both uniforms complemented (`1 − u`);
  `Cauchy` is `location + scale·tan(π(u − ½))`; F is redrawn while ≤ 0 and cut at 1.
