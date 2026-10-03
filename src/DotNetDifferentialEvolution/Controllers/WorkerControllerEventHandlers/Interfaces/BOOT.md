# BOOT.md — pass-loop handler contract

## Purpose

The callback through which one worker (the master) runs the orchestration on its own
thread at the generation barrier, so there is no separate orchestrator thread.

## Invariants

- **Only the master worker has a handler.** Held by `DifferentialEvolution`'s
  construction (package root), which passes it to the last worker only.

## Dependencies

- [Controllers](../../API.md) — `WorkerController`, the handler's argument, declared in
  that ancestor's directory.

## Constraints

Inherited from the parent ([BOOT.md](../BOOT.md)). In addition: none.

## Acceptance criteria

- [x] The orchestrator is reached through it every generation: 2026-10-02,
      `WorkersOrchestratorTests` (integration, full local run, 76 of 76).
- [ ] ⚠ The contract references its grandparent's concrete `WorkerController`, so the
      `Controllers` subtree depends on itself in both directions.

## Taboos

- **No removal as "vestigial".** The orchestrator's handler chain was removed in
  `925a63f`, but this interface was kept on purpose: it is how the master worker
  reaches the orchestrator.
