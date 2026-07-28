# Changelog

Notable changes to DotNetDifferentialEvolution. Versions follow [semantic versioning](https://semver.org/).

The API-break lists below are not written by hand: package validation diffs the packed assembly
against the previously released package on every build, and the differences it finds are recorded
in `src/DotNetDifferentialEvolution/CompatibilitySuppressions.xml`. Behavioural changes are a
different matter — nothing can detect those automatically, so they are called out explicitly.

## 5.1.0

Everything that breaks a consumer, plus the performance work. There is no 5.0.0: the breaking
changes and the performance changes were developed as separate branches and released together.

### ⚠ A seed no longer reproduces a 4.x run

This changes no signature, so no tool can warn about it. If you rely on `WithSeed` to reproduce a
specific run recorded under 4.x, that run cannot be reproduced under 5.1.0. Three independent
causes, none of which can be avoided while keeping the speedup:

- the generator changed from `System.Random` to xoshiro256\*\*;
- the per-gene crossover test changed shape, comparing 64-bit integers instead of doubles;
- the Gaussian draw now consumes two uniforms per *pair* of normals rather than per normal.

Seeds remain reproducible within 5.1.x, and across workers and thread schedules, exactly as before.

### Breaking

- **`ISelectionStrategy` is one method again.** `Select` returns `bool`; the separate `SelectTrial`
  added in 4.1.0 is gone. There is no longer a way to report an outcome other than the one
  performed.
- **`IGenerationStrategy.AfterGeneration` receives a `GenerationContext`** — a narrowed view — in
  place of the whole `ProblemContext`.
- **`ProblemContext`'s raw `Memory<double>` members are replaced by `PopulationView`**, which
  carries the genes, the fitness values, the active count and the genome size as one value. This
  removes the "which length is authoritative?" question that produced the `PopulationSize` defect
  fixed in 4.1.0.
- **`AdaptiveStrategyBase.UpdateArchive` / `RebuildSortedIndices`** and the `AfterGeneration`
  overrides on the jDE, JADE and SHADE strategies follow the context change.
- **`OrchestratorWorkerHandler`'s constructor** no longer takes the vestigial handler chain.

17 differences in total; the full list is in `src/DotNetDifferentialEvolution/CompatibilitySuppressions.xml`.

### Performance

Single-worker throughput on a cheap objective (N=300, D=20, one generation) improves **1.71×**
against the 4.x default path — 117.7 µs to 69.0 µs. Two things are worth knowing before reading
that number as a promise:

- **End to end it is smaller.** On 30-D Rastrigin over 600k evaluations: 1.26–1.53× at one worker,
  1.11–1.33× at two to eight.
- **It shrinks as the objective gets more expensive**, to nothing. This work optimises the case
  where DE is already cheapest to run. If your objective dominates, it will not help you.

What changed: each worker now draws from its own xoshiro256\*\* instance instead of sharing one
thread-static `System.Random`; the draws are made through a type the JIT can devirtualise and
inline rather than through a virtual call per gene; the per-gene crossover test compares integers
instead of converting to a double first; and the Box–Muller transform keeps the second normal it
produces instead of discarding it (34–39 ns to 22–24 ns per Gaussian draw, though this is not
measurable end to end — the adaptive variants draw twice per individual against thirty crossover
tests).

**The algorithm is unchanged**, and this was checked rather than assumed: 100 runs per side of
DE/rand/1/bin on 10-D Rastrigin give Welch t = 0.097 and Mann–Whitney z = −0.204. At that sample
size the test would detect a shift of 7.3% of the mean.

### Build and tooling

- Package validation runs against the previously released package on every build, and CI packs so
  the public-surface diff is seen on a pull request rather than first at tag time.
- The benchmark project no longer needs `DOTNET_ROLL_FORWARD=Major` set by hand.
- The C# language version is pinned rather than inferred from the target framework.
- `dotnet build` no longer produces a NuGet package as a side effect.
- NuGet audit warnings no longer fail the build, so an advisory published against a dependency
  cannot block a release on its own.

## 4.1.0

Every fix and extension point that does not break a consumer. A mathematical audit of the library
found **no error in any core formula** — the mutation arithmetic, binomial crossover, bound repair,
Gaussian and Cauchy sampling, JADE/SHADE/L-SHADE adaptation, the LPSR schedule, archive semantics,
p-best selection, evaluation accounting and all 14 benchmark functions were verified correct.
Everything below is an integration, API-surface or paper-fidelity defect. All 151 tests in the
4.0.0 suite passed with every one of these defects present; the suite is now 251.

### Fixed

- **p-best ranking no longer freezes at generation 0.** Using `CurrentToPBestMutationStrategy`
  directly — without one of the adaptive presets — left `FitnessSortedIndices` sorted once at build
  time and never refreshed, so `x_pbest` was drawn for the whole run from whichever individuals
  ranked highest in the *initial random population*. Measured on a 5-D sphere: 38.8 of 40 ranking
  positions wrong. The engine now re-ranks whenever the mutation strategy declares it needs a
  ranking.
- **A missing control-parameter provider no longer turns the run into a no-op.** It is now a
  configuration error at build time instead of a silent zero mutation force.
- **A NaN fitness value is no longer absorbing.** One NaN could previously propagate until it
  dominated the run.
- **`PopulationSortHelper` ranked a NaN individual as the best.** NaN now ranks last.
- **`Population.PopulationSize` reports the active size, not the allocated one.** Under L-SHADE's
  linear population reduction it reported 50 in every generation while the true size fell 49 → 4,
  and iterating handed back 46 stale individuals. `Capacity` now reports the allocation.
- **p-best pool floor raised from 1 to 2**, matching reference L-SHADE.
- **Round-half-up where the papers specify it**, replacing .NET's banker's rounding in the
  population-size schedule.

### Added

- **`WithSeed(int)` — reproducible parallel runs.** Each worker gets its own generator derived from
  the seed and the worker index, so a seeded run reproduces regardless of thread scheduling. A seed
  reproduces a run only within a minor version; see 5.1.0.
- **`WithVariant(IDeVariant)` — a DE variant is now a first-class extension point.** The four
  presets became thin wrappers over it, and a third-party variant gets the same integrity checks as
  a built-in.
- **`CancellationToken` on `RunAsync`.** Cancellation is observed at the generation barrier, so a
  run with an expensive objective stops within roughly one generation rather than instantly, and
  the workers are actually stopped rather than left running behind an abandoned task.
- **Mutation strategies declare their requirements as data**, which is what lets the engine
  provision a ranking without being told twice.
- **The selection strategy reports its own decision** instead of having the executor infer it.

### Changed

- **The generation barrier waits cooperatively instead of spinning.** This is a cliff, not a
  politeness fix: the raw spin cost roughly 101× on an oversubscribed machine.

### API changes

Two members were added to the fluent builder interfaces — `IDifferentialEvolutionBuilder.WithSeed`
and `IMutationStrategyRequired.WithVariant`. Package validation reports added interface members as
breaking, which is correct in the abstract: it breaks anyone implementing those interfaces by hand.
The fluent step interfaces exist only to be returned by `DifferentialEvolutionBuilder`, so in
practice there is nothing to break. No other difference from 4.0.0 — including binary
compatibility, which `RunAsync` briefly lost and got back before release.
