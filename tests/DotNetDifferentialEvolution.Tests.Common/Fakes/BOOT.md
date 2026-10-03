# BOOT.md — Tests.Common/Fakes

## Purpose

Crossover, index selection, distribution sampling and parameter adaptation are random by
design. A unit test of them needs either every draw dictated (`ScriptedRandomProvider`)
or a repeatable stream (`DeterministicRandomProvider`). This node supplies both.

## Invariants

- **An unscripted draw is a failure, not a default.** An empty or exhausted queue
  throws unless cycling was asked for, so a test cannot silently depend on a draw its
  author did not predict.
- **A scripted `Next` value must lie in the requested range.** An out-of-range value
  throws with the range in the message. `f7887ab` uses this on purpose: a deliberately
  out-of-range draw makes the p-best pool size readable without new public API.
- **The two queues are independent.** `Next` never consumes a `NextDouble` value or the
  reverse.

## Dependencies

None.

Outside the tree: `DotNetOptimization.Abstractions` 1.0.0 (`BaseRandomProvider`).

## Constraints

Inherited from the parent ([BOOT.md](../BOOT.md)). In addition:

- A fake is single-threaded; tests that use it drive the code under test on one thread.

## Acceptance criteria

- [x] Used where it is needed: 2026-10-02, `ScriptedRandomProvider` in 11 unit-test
      files (the four adaptive strategies, both control-parameter providers,
      `CurrentToPBestMutationStrategyTests`, `CrossoverHelperTests`,
      `RandomIndexSelectorTests`, `RandomDistributionHelperTests`,
      `SeededRandomProviderGaussianTests`); `DeterministicRandomProvider` in
      `RandomIndexSelectorTests` only.
- [ ] ⚠ The fakes have no tests of their own: their throwing rules are exercised only
      through the tests that use them.

## Taboos

- **No silent default for an unscripted draw.** A fake that returned 0 when it ran out
  would let a test pass against a draw sequence nobody wrote down.
- **No `CycleWhenExhausted` as a convenience** where the test can count its draws: it
  hides how many draws the code made.
