# BOOT.md — DotNetDifferentialEvolution (CPU package)

<!-- Started early, in slice 3, because its children link here for their frame. It holds
     only package-level facts read from the csproj, the build and the README. The
     synthesis of the children (## Decomposition, the engine's invariants, the package
     API) is slice 5 of the root's ## Reconstruction. -->

## Purpose

The CPU Differential Evolution package, `DotNetDifferentialEvolution` on nuget.org
(5.1.0 in the csproj): multi-threaded, SIMD-accelerated, classic DE with several
mutation schemes plus jDE, JADE, SHADE and L-SHADE, built through a fluent builder. The
objective contract and the solution type come from the shared
`DotNetOptimization.Abstractions` package, so one objective drives every optimizer of
that family.

## Invariants

- **Every public member carries XML documentation.** `GenerateDocumentationFile` is on
  and warnings are errors, so a missing comment fails the build. Held by the build.
- **The public surface is diffed against the last released baseline (4.0.0) on every
  pack.** Deliberate breaks are listed, machine-generated, in
  `CompatibilitySuppressions.xml` (19 entries on 2026-10-02). Held by package
  validation and the CI "Pack" step.
- **Internals are visible to the unit-test assembly only** (`InternalsVisibleTo
  DotNetDifferentialEvolution.UnitTests`), so internal helpers are tested directly
  without becoming public.

## Dependencies

None.

Outside the tree: `DotNetOptimization.Abstractions` 1.0.0 (imported as a global using);
.NET 8.

## Constraints

Inherited from the root ([BOOT.md](../../BOOT.md)). In addition:

- C# 12, `net8.0`, the repository's full analyzer policy with no package-specific
  relaxations.
- The package ships `README.md` and, under `docs/`, `ALGORITHMS.md` and
  `AGENT_GUIDE.md`; SourceLink with an embedded PDB.

## Acceptance criteria

- [x] The package builds with 0 warnings and its unit and integration suites pass:
      2026-10-02, local run of the CI filters (232 unit, 70 integration).
- [ ] The children are described (root `## Reconstruction`, slices 3 to 5).
- [ ] ⚠ The validation baseline is 4.0.0 although 4.1.0 and 5.1.0 shipped; the csproj
      explains that 4.1.0 was not yet downloadable when it was set, and the suppression
      file therefore folds the 4.1.0 changes in a second time.

## Taboos

- **No `GeneratePackageOnBuild`.** A package from a local branch carries a SourceLink
  map to a commit that may never be pushed (`ecd8f09`, 2026-07-28).
- **No hand-written list of breaking changes.** Regenerate `CompatibilitySuppressions.xml`
  with `ApiCompatGenerateSuppressionFile=true`; it is what release notes are written
  from (csproj comment, `ecd8f09`).

## Decomposition

To be synthesized in slice 5. Children described so far: see `API.md`, `## Children`.
