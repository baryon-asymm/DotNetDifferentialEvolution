# Changelog — DotNetDifferentialEvolution.GPU

Notable changes to the GPU package, released from `gpu-v*` tags. Versions follow
[semantic versioning](https://semver.org/). The release workflow takes the notes of a version
from its `## <version>` section below.

## 1.1.0

A new form of objective, and the fixes of two audits; code written for 1.0 compiles unchanged.

- **`IGpuPointwiseFitnessFunction<TPoint>`**, built with
  `GpuDifferentialEvolutionBuilder.ForPointwiseFunction(function, pointCount)`: an
  objective made of `P` independent parts. `EvaluatePoint(genes, point)` returns one part's
  result (any unmanaged struct), each in its own GPU thread, and `Combine(genes, points)`
  turns an individual's `P` results into its fitness. A generation is then three launches
  instead of one, and lasts as long as one part instead of all of them. The rest of the
  builder (schemes, variants, stop rules, devices, observer) is shared.
- The builder's stage interfaces now constrain `TFunction` to `struct` only, so both
  entry points use them; code written for 1.0 compiles and runs unchanged.

Behaviour changes a caller can see (nothing else changes; reviewed by a consumer, 2026-10-10):

- **Seeded runs reproduce 1.0.1 bit for bit**, on the CPU accelerator and on CUDA, through
  `OnDevice` and `OnAccelerator` alike (48 runs compared: result and every snapshot).
- **Cancellation is unchanged:** a cancelled token ends the task as canceled
  (`OperationCanceledException` when awaited), also when cancelled from the observer, and a
  `Dispose` after it throws nothing unless a release fails.
- **`Dispose` throws an `AggregateException`** of every failed release, after running them
  all; 1.0.1 threw the first failure and skipped the rest.
- **An exception on the run's thread faults the task** whatever its type; before, some could
  end the process.
- **`Build` does more:** it loads every kernel on a thread of its own (the caller's
  `Accelerator.Current` is left as it was), and on CUDA and OpenCL a configuration that
  ranks (JADE, SHADE, L-SHADE) with N above 1 024 times two ranking methods once, a few
  milliseconds.
- **New refusals** (below): sizes and point types that could not run correctly before.
- **An objective type ILGPU cannot see** (private nested, or internal without
  `[assembly: InternalsVisibleTo("ILGPURuntime")]`) now fails `Build` with an
  `InvalidOperationException` that names the type and the remedies; ILGPU's "Access is
  denied" is its inner exception.

Fixes and speed-ups from two audits of the package, no other public signature change:

- **Kernels fill the device.** Each kernel is loaded with a group size fitted to how many
  threads it launches, instead of ILGPU's default: a costly objective at N = 1 024 went from
  29.4 to about 3.8 ms per generation on an RTX 5070 Ti.
- **Two optimizers on one accelerator no longer share a kernel.** Every kernel is compiled for
  its own optimizer; on OpenCL, disposing one optimizer used to break or crash another one
  running on the same accelerator.
- **Faster work between generations.** Ranking (JADE, SHADE, L-SHADE) compares integer keys,
  and `Build` times ranking by counting against the bitonic network once on CUDA and OpenCL,
  since where one overtakes the other depends on the device (a few milliseconds, only above
  N = 1 024). The best-index and archive passes run in chunks of about √N (at N = 46 080 the
  best index went from 144 to 80 µs). With a stagnation limit the stop word is copied
  without stopping the device and read 16 generations later; the run still ends at the
  generation the rule fired. Results are unchanged.
- **`Dispose` is exception-safe.** Every release runs even when one throws, then the failures
  are thrown together as an `AggregateException`. A second `Dispose`, also a concurrent one,
  waits for the first. A failed `Build` throws its own exception, with any release failures in
  its `Data["DotNetDifferentialEvolution.GPU.ReleaseFailures"]`. Any exception on the run's
  thread, `OutOfMemoryException` included, faults the task instead of ending the process.
- **`Build` leaves the calling thread's `Accelerator.Current` as it found it.**
- **New argument checks:** a point type with fields other than numeric primitives, their
  enums or such structs, or packed below its natural size (`ArgumentException`); N, or N·P,
  above `int.MaxValue − 1 023` (`ArgumentOutOfRangeException`); JADE, SHADE or L-SHADE above
  N = 2³⁰ (`InvalidOperationException` from `Build`). Such sizes could not run correctly before.
- **A pointwise objective and its monolithic twin** give the same run bit for bit on the CPU
  accelerator; on a GPU the device compiler may fuse a multiply and an add of the monolithic
  form, so values can differ in the last bits.

## 1.0.1

A fix to SHADE and L-SHADE on the device; no API change.

- **The memory no longer fills with NaN when the improvements overflow the sums.** An objective
  that scores its infeasible points `double.MaxValue` gave improvements that are finite but sum to
  `+∞`; `∞/∞` wrote NaN into the memory and the mutants, and the best individual, held NaN genes
  (seen on a GPU: 26 NaN genes of 32). The CPU package had the same defect (its 6.0.1). A pass
  finds each chunk's largest weight, and the weights are divided by the generation's largest when
  it exceeds `double.MaxValue / (2·N)`, as the CPU package now does; below that bound the sums are
  unchanged. SHADE and L-SHADE launch one more small kernel per generation. Found by
  PastyPropellant, 2026-10-07.

## 1.0.0

A new library under the old name: every public type of 0.x (0.1.0, 0.0.2 and 0.2.0) is gone.

- **The builder** replaces the hand-assembled `KernelController`, the strategy structs and the
  `XorShift32` states: `GpuDifferentialEvolutionBuilder.ForFunction(objective)`, then bounds,
  population size, scheme, stop rule and device.
- **The CPU package's schemes and variants**, by its names, parameters and defaults, with its
  semantics draw for draw: `WithDefaultMutationStrategy` (rand/1), `WithBestMutationStrategy`,
  `WithCurrentToBestMutationStrategy`, `WithRandTwoMutationStrategy`,
  `WithBestTwoMutationStrategy`, `WithJde`, `WithJade`, `WithShade`, `WithLShade`.
- **Selection** keeps each variant's paper rule for a tie: jDE (Brest et al. 2006) and JADE
  keep the parent, the other configurations take the trial.
- **Stop rules:** `WithGenerationLimit`, `WithEvaluationLimit`, `WithStagnationLimit`.
- **The objective** is a struct implementing `IGpuFitnessFunction`, compiled into the kernel; it
  returns its value instead of writing into the population.
- **Runs are seeded** (Philox4x32-10, the same draws on every backend) and reproducible on one
  device; `RunAsync` no longer blocks and observes a cancellation token.
- **The result** is an `ISolution` (`DotNetOptimization.Abstractions`).
- **Devices:** CUDA (math through libdevice; a CUDA Toolkit is needed), OpenCL, or ILGPU's CPU
  accelerator; `Auto` falls back and says why. ILGPU is pinned to 1.5.3.
- `Dispose` no longer forces a garbage collection.
