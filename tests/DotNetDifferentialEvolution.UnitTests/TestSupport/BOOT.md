# BOOT.md — UnitTests/TestSupport

## Purpose

Small `Population` instances for tests of the parts that read one (termination rules,
the cursor) without running the algorithm. It holds no tests.

## Invariants

- **The population is backed by the caller's arrays.** The stagnation tests rely on
  this: they lower `fitnessValues[0]` between calls to simulate generations.

## Dependencies

- [Models](../../../src/DotNetDifferentialEvolution/Models/API.md) — `Population`.

## Constraints

Inherited from the parent ([BOOT.md](../BOOT.md)). In addition:

- `internal`: nothing outside this test project uses it; the shared library has its
  own helpers.

## Acceptance criteria

- [x] Used by `TerminationStrategies` and `Models` tests: 2026-10-02, by search.
- [ ] ⚠ The default best index is found with `<`, which ignores `NaN`: a `NaN` first
      value would be reported best. No current caller passes `NaN`.

## Taboos

- **No test logic here.** A helper that asserts hides the assertion from the test.
