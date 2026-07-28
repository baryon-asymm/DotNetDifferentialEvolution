# Changelog

Notable changes to DotNetDifferentialEvolution. Versions follow [semantic versioning](https://semver.org/).

The API-break lists below are not written by hand: package validation diffs the packed assembly
against the previously released package on every build, and the differences it finds are recorded
in `src/DotNetDifferentialEvolution/CompatibilitySuppressions.xml`. Behavioural changes are a
different matter — nothing can detect those automatically, so they are called out explicitly.

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
