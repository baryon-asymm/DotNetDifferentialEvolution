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

A fourth cause applies to **L-SHADE only**: its `M_CR` update was corrected (see *Fixed* below).
Every parameter drawn after the first memory update differs, so an L-SHADE run diverges from its
4.x counterpart even setting the three causes above aside.

Seeds remain reproducible within 5.1.x exactly as before: for a given worker count, and regardless
of how the workers' threads interleave. They were never reproducible *across* worker counts —
individual `i` draws from worker `i mod W`'s stream — and two comments that claimed otherwise have
been corrected.

### Fixed

- **L-SHADE updated `M_CR` with the wrong mean.** It inherited SHADE (2013)'s weighted *arithmetic*
  mean, but L-SHADE is built on SHADE 1.1, whose memory update specifies the weighted *Lehmer* mean
  — the same one already used for `M_F`. Only half of that rule had been implemented: the terminal
  `M_CR` value comes from the same algorithm and was already there.

  The error had a fixed sign. `mean_WL − mean_WA = Var_w/E_w ≥ 0`, so the arithmetic mean always
  reported the lower value — precisely the downward bias on `M_CR` that the Lehmer mean exists to
  remove — and it compounded with the terminal rule, since a lower `M_CR` makes the CR = 0 lock
  more likely. Plain SHADE is unaffected: the 2013 paper does specify the arithmetic mean, so
  `ShadeStrategy` was correct and is unchanged.

  This is the one the 4.1.0 audit missed; that section's "JADE/SHADE/L-SHADE adaptation verified
  correct" should be read as covering JADE and SHADE.
- **`LShadeStrategy` accepted arguments that produced a wrong run rather than an error.** An
  evaluation budget of `0` divided to a non-finite reduction schedule and collapsed the population
  to its floor in the first generation, silently; a negative archive rate produced a negative
  archive capacity that slipped past the "archive disabled" guard and threw from inside a
  generation. Both are now rejected by the constructor, and the archive guard treats any
  non-positive capacity as a disabled archive. Reachable only by constructing the strategy
  directly — `WithLShade` already validated both.

### Breaking

- **`ISelectionStrategy` is one method again, and it now reports a `SelectionOutcome`.** The
  separate `SelectTrial` added in 4.1.0 is gone, so there is no longer a way to report an outcome
  other than the one performed. `Select` returns `SelectionOutcome` — `ParentKept`,
  `TrialAccepted` or `TrialImproved` — instead of `bool`, because survival and improvement are two
  different questions with two different consumers (see *Selection now follows the papers* below).
- **`TrialRecord.Succeeded` is replaced by `TrialRecord.Outcome`**, with `Replaced` and `Improved`
  as derived predicates. A custom generation strategy reading `Succeeded` should read `Improved`
  if it credits parameters or archives parents, and `Replaced` if it tracks what is in the
  population.
- **`IGenerationStrategy.AfterGeneration` receives a `GenerationContext`** — a narrowed view — in
  place of the whole `ProblemContext`.
- **`ProblemContext`'s raw `Memory<double>` members are replaced by `PopulationView`**, which
  carries the genes, the fitness values, the active count and the genome size as one value. This
  removes the "which length is authoritative?" question that produced the `PopulationSize` defect
  fixed in 4.1.0.
- **`AdaptiveStrategyBase.UpdateArchive` / `RebuildSortedIndices`** and the `AfterGeneration`
  overrides on the jDE, JADE and SHADE strategies follow the context change.
- **`OrchestratorWorkerHandler`'s constructor** no longer takes the vestigial handler chain.

19 differences in total; the full list is in `src/DotNetDifferentialEvolution/CompatibilitySuppressions.xml`.

### Selection now follows the papers

A trial survives when it is **at least as good** as its parent, and counts as a **success** only
when it is strictly better. Previously both used the strict comparison, so a tie kept the parent.

This is the rule the papers specify, and they specify the two thresholds separately for a reason:
SHADE (2013) Eq. (6) and L-SHADE (2014) Algorithm 2 line 12 accept the trial on `f(u) ≤ f(x)`,
while line 16 records the success on `f(u) < f(x)`. Accepting ties is what lets a population drift
sideways across a plateau instead of standing still on it; keeping the success record strict is
what stops a run of zero-gain ties from dragging the parameter adaptation with them.

What consumes which:

| | rule | consumers |
|---|---|---|
| survived | `f(u) ≤ f(x)` | the next generation; jDE's parameter inheritance |
| improved | `f(u) < f(x)` | the external archive; JADE/SHADE/L-SHADE adaptation (`S_CR`, `S_F`) |

Two NaNs are not a tie: swapping one unusable value for another is not an acceptance. A NaN parent
is still worse than any real value, and is still replaced by one.

This affects every variant, classic DE included, and only on objectives that actually produce
ties — which is why it is called out here rather than left to the API-break list.

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

### Documentation

- **`docs/ALGORITHMS.md` — a specification of what the library computes.** Every variant is stated
  in the notation of the paper it comes from, cited down to the equation number or algorithm line,
  next to the type that implements it. It exists because of the L-SHADE `M_CR` bug above: the code
  was clear and the tests were green, but nothing in the repository *stated* which mean the paper
  specifies, so there was nowhere the wrong one could be noticed.

  Its last section lists every deliberate deviation from a literal reading of the papers — midpoint
  bound repair applied to all strategies, NaN ordering, the p-best pool floor of 2, round-half-away
  -from-zero, the evaluation count starting at `N`, and reproducibility holding only per worker
  count — which is the section to read before comparing results against a reference implementation.

  CI checks that the file paths it references still exist, so a rename cannot silently orphan them.

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
