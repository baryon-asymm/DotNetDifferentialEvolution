# BOOT.md — mutation contract

## Purpose

The strategy seam of the CPU engine, with a declaration of needs attached: a strategy
says, as data, which parts of the context it reads, and the builder and the engine act on
that instead of handing over a context the strategy cannot use.

## Invariants

- **Needs are declared, and a declaration is enforced.** `ControlParameters` without a
  provider is refused at build time; `FitnessRanking` makes the engine re-rank every
  generation (`7a7649f`). Held by `MutationRequirementsValidationTests` (builder) and
  `FitnessRankingMaintenanceTests` (engine).
- **The default declaration is `ControlParameters`.** Reading F and CR from the context
  is the normal shape of a strategy, and a missing provider is the failure worth catching
  at build time. Held by the interface's default member.
- **`MinimumPopulationSize` bounds the distinct-index search.** The builder validates
  the population size against it, so the search always terminates. Held by the builder
  (slice 5) and the strategies' overrides.

## Dependencies

- [MutationStrategies](../API.md) — `MutationContext`, the parameter of `Mutate`,
  declared in the parent's directory.

## Constraints

Inherited from the parent ([BOOT.md](../BOOT.md)). In addition:

- Default interface members are part of the contract; changing a default changes every
  third-party strategy that relies on it.

## Acceptance criteria

- [x] Declarations are validated and maintained: 2026-10-02,
      `MutationRequirementsValidationTests` (unit) and `FitnessRankingMaintenanceTests`
      (integration), local run (part of 115 and 22 for slice 4).
- [ ] ⚠ `BestIndividual` changes nothing in the engine today; it is documentation that
      looks like a switch.

## Taboos

- **No undeclared need.** A strategy that reads F/CR without declaring it gets `NaN` and
  a run that "completes normally" with the best of the initial sample: measured before
  `7a7649f`, 6000 of 6000 trial genomes `NaN` over 200 generations.
- **No ranking maintained by the variant alone.** Before `7a7649f` only JADE/SHADE
  re-ranked, so a hand-wired current-to-pbest run drew p-best from the initial
  population's ranking for the whole run (38.8 of 40 positions wrong on average).
