# BOOT.md — Models/Interfaces

## Purpose

The two interfaces behind the zero-copy cursor of the result population: the cursor
accepts an updater, the updater writes the cursor's fields by reference. Their only
implementations are in the parent node (`IndividualCursor`, `Population`).

## Invariants

- **The updater receives the cursor's fields by `ref` and slices, never copies, the
  genes.** Held by the signature (`ref ReadOnlyMemory<double>`) and the parent's
  implementation.

## Dependencies

None.

## Constraints

Inherited from the parent ([BOOT.md](../BOOT.md)). In addition: none.

## Acceptance criteria

- [x] Exercised through the parent's cursor: 2026-10-02, `PopulationTests` and
      `IndividualCursorTests` (local run, part of 115 of 115 unit cases for slice 4).
- [ ] ⚠ A double-dispatch pair for one implementation each; consumers see it only as
      `IndividualCursor.AcceptUpdater`, which they have no reason to call.

## Taboos

- **No copy of genes in an updater.** The cursor exists to read an individual without
  allocating; `GetSnapshot(deepCopy: true)` is the one place a copy is made on request.
