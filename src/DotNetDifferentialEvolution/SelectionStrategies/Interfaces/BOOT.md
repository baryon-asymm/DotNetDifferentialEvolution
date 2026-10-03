# BOOT.md — selection contract

## Purpose

The seam where a variant decides survival. One method both writes the survivor and
reports the decision, so that the population and what the adaptive machinery learns
from it cannot disagree.

## Invariants

- **One method writes and reports.** There is no second member describing a decision
  made elsewhere. Held by the shape of the interface, deliberately collapsed to one
  method in `c7c4f1f` (2026-07-28).
- **The report separates survival from improvement** through `SelectionOutcome`, so a
  trial cannot improve without replacing. Held by the type (an enumeration, not two
  booleans), owned by the parent node.

## Dependencies

- [SelectionStrategies](../API.md) — `SelectionOutcome`, the return type, declared in
  the parent's directory.

## Constraints

Inherited from the parent ([BOOT.md](../BOOT.md)). In addition:

- Called on every worker for every individual of every generation: no allocation.

## Acceptance criteria

- [x] The built-in implementation honours the contract (writes and reports the same
      decision, ties and `NaN` included): 2026-10-02, `SelectionStrategyTests`, local
      run (part of 45 of 45 for slice 3).
- [ ] No test checks the contract against a third-party implementation, e.g. that the
      engine trusts the returned outcome rather than recomputing it.

## Taboos

- **No second member that reports a decision.** Two methods that must agree are the
  shape that let them disagree: `SelectTrial` beside `Select` (`68f3a92`) was removed
  again in `c7c4f1f`.
- **No outcome other than the one written.** It desynchronises the archive and the
  JADE/SHADE/L-SHADE adaptation from the population (`68f3a92`).
