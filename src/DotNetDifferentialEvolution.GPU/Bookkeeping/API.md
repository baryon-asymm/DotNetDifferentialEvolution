# API.md — DotNetDifferentialEvolution.GPU/Bookkeeping

Namespace: `DotNetDifferentialEvolution.GPU.Bookkeeping`. Internal to the assembly;
visible to the GPU test project and to ILGPU's runtime assembly. Consumed by the
package root.

## Internal to the assembly ⏳

Designed 2026-10-05; the signatures are written here when built.

```csharp
internal static class FitnessOrder           // the one total order (S9, S10)
{
    public static double KeyOf(double fitness);                              // NaN → +∞
    public static bool Precedes(double keyA, int indexA, double keyB, int indexB);
}

internal static class AdaptationRules        // host and device (S7)
{
    // JADE: μCR and μF from S_CR, ΣF and ΣF² over the improved trials.
    // SHADE / L-SHADE: one memory slot from Σw, ΣwCR, ΣwCR², ΣwF, ΣwF², max CR.
}

internal static class ArchiveRules           // host and device (S8)
{
    // The slot of an improved parent from its fill position, the capacity and its draws.
}

internal static class LShadeSchedule         // host (S11)
{
    // The planned N from the evaluation count; the archive capacity round(rate·N).
}

internal static class StagnationRule         // host and device (S12)
{
    // last, streak and stop from the best value, as StagnationStreakTerminationStrategy.
}

internal static class BookkeepingKernels     // one entry point per pass, Index1D first
{
    // best index: per chunk, then over the chunks;
    // ranking: by counting, or sort keys then one bitonic step per launch;
    // archive: improved per chunk, the scan and the new size, the slots, the copy;
    // adaptation: sums per chunk, then the update;
    // L-SHADE: the survivors in ranking order; the stop rule.
}

internal sealed class GenerationBookkeeping : IDisposable
{
    // Allocates the buffers and loads the kernels a configuration needs, and enqueues
    // the passes after each generation in the CPU package's order.
}
```

- Every kernel reads the stop word first and returns when it is set.
- Chunks are 1 024 individuals; ranking by counting up to N = 8 192, the bitonic
  network above it, over N rounded up to a power of two with +∞ keys at the end.
- The control block of the stop rule is the only buffer the host reads during a run:
  every 16 generations and before each observer call.
