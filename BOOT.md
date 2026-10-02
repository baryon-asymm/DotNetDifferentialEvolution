# BOOT.md — DotNetDifferentialEvolution (tree root)

<!-- Brownfield reconstruction in progress (AGENTS.md §9). This file records the state
     of the repository as it is, not as it should be. Sections marked "to be synthesized"
     are filled bottom-up once the slices under them are described. -->

## Purpose

The repository builds two NuGet packages of Differential Evolution (DE) for .NET:

- `DotNetDifferentialEvolution` (`src/DotNetDifferentialEvolution`, version 5.1.0 in
  its csproj): CPU, multi-threaded, SIMD; classic DE plus jDE, JADE, SHADE and L-SHADE.
  It depends on the shared `DotNetOptimization.Abstractions` package for the objective
  contract and the solution type.
- `DotNetDifferentialEvolution.GPU` (`src/DotNetDifferentialEvolution.GPU`, version
  0.0.2 in its csproj): DE on a GPU through ILGPU. Imported into this repository with
  its history on 2026-10-02 (merge `fe13623`) from the separate repository
  `baryon-asymm/DotNetDifferentialEvolution.GPU`.

The two packages are siblings: neither references the other.

Not goals: asynchronous or GPU evaluation in the CPU package (`docs/AGENT_GUIDE.md`,
"What it does not do"); constraints beyond box bounds; integer or variable-length
genomes; multi-objective optimization.

## Invariants

- **Every build is warnings-as-errors with the full analyzer set** (`latest-all`,
  code style enforced). Held by `Directory.Build.props`, imported by every project; a
  rule is relaxed only in `.editorconfig`, scoped to a path, with its reason.
- **The CPU package does not depend on ILGPU or on the GPU package.** Held by the
  project references of `src/DotNetDifferentialEvolution`; the owner's decision of
  2026-10-02, recorded at the import.
- **The CPU package's public surface is diffed against its last release on every CI
  run.** Held by package validation (`EnablePackageValidation`, baseline 4.0.0) and the
  CI "Pack" step; deliberate breaks are listed in `CompatibilitySuppressions.xml`.

## Dependencies

None.

Outside the tree: .NET SDK 8 and 10 (CI installs both); `DotNetOptimization.Abstractions`
1.0.0 (CPU package); ILGPU and ILGPU.Algorithms 1.5.1 (GPU package); xUnit 2.5.3,
Microsoft.NET.Test.Sdk 17.8.0, coverlet 6.0.0 (tests); BenchmarkDotNet 0.14.0 (benchmarks);
Python 3.8+ (`tools/protocol-lint`).

## Constraints

- Shipping code targets `net8.0` with C# 12 pinned (`LangVersion` 12); test projects
  use `latest` and `RollForward=Major` (`tests/Directory.Build.props`).
- CI (`.github/workflows/ci.yml`) runs on `ubuntu-latest`: documentation references,
  build, `Category=Unit`, then everything except `Category=Slow` and `Category=Gpu`,
  then a throw-away pack of the CPU package for the API check. Hosted runners have no
  OpenCL device, so the GPU tests run only on a developer machine.
- Releases: `.github/workflows/release.yml` publishes on a `v*` tag and packs the CPU
  package only.
- There is no external ancestor: the tree root is the repository root. The loader
  (`CLAUDE.md`) carries no subject-matter claims (AGENTS.md §2). `README.md` and
  `docs/*.md` are consumer documentation shipped in the package, not part of the tree.

## Acceptance criteria

- [x] The solution builds with 0 warnings and 0 errors in Release: 2026-10-02,
      `dotnet build DotNetDifferentialEvolution.sln -c Release` (local, Windows 11,
      .NET SDK 10.0.112).
- [x] The CI test gates pass: 2026-10-02, local run of the CI filters: `Category=Unit`
      232 passed; `Category!=Slow&Category!=Gpu` 232 + 70 passed, exit code 0 for both.
- [x] The GPU tests pass on a machine with an OpenCL device: 2026-10-02,
      `tests/DotNetDifferentialEvolution.GPU.Test`, 2 of 2 passed (local).
- [ ] The tree passes `protocol_lint` without errors. Red until the reconstruction
      below is finished; the linter is not in CI until then (AGENTS.md §13).
- [ ] The reflection checks are written for this stack and each is proven
      non-degenerate (AGENTS.md §13). ⚠ The installed kit copy has an empty
      `reference/dotnet/`; the reference implementation has to be obtained first.
- [ ] ⚠ The GPU package has no release path: `release.yml` packs only the CPU package
      and both would share the `v*` tags.
- [ ] ⚠ The GPU package's csproj says 0.0.2 while nuget.org carries 0.1.0, 0.0.2 and
      0.2.0; 0.2.0 was published from a version never committed to git.
- [ ] ⚠ The GPU tests run in no CI: hosted runners have no OpenCL device.

## Taboos

- **No ILGPU (or any GPU runtime) in the CPU package.** Every CPU consumer would carry
  a native GPU runtime it does not use (owner, 2026-10-02).
- **No `GeneratePackageOnBuild`.** A package built from a local branch carries a
  SourceLink map to a commit that may never be pushed; packages are packed only from a
  tagged commit (`ecd8f09`, 2026-07-28; the CPU csproj explains it).
- **No blanket analyzer suppression.** A rule is turned off only in `.editorconfig`,
  scoped to the paths it does not fit, with the reason written beside it.
- **No push, tag, publish or merge into `main` without the owner's word**, each time.

## Decomposition

To be synthesized when the slices below are described. As it stands: one directory per
package under `src/`, test projects under `tests/` (the CPU package has unit,
integration and shared-support projects; the GPU package has one), benchmarks under
`benchmarks/`, the protocol linter under `tools/protocol-lint/`.

## Reconstruction

Temporary section (prompt 02): the memory between slices. Removed when the tree is
reconstructed. Node list from `protocol_lint.py --list-nodes`, 2026-10-02: 75 nodes.
Couplings estimated textually (type names declared in one directory and used in
another, method bodies included); the reflection checks will replace the estimate.

Findings of the inventory, for the slices to record in their nodes:

- Namespaces match directory paths in every directory (checked 2026-10-02).
- ⚠ One cycle, CPU package: `Models` ↔ `GenerationStrategies`, `Interfaces`,
  `LocalSearch`, `TerminationStrategies/Interfaces`. Confirmed on the code in slice 4:
  `ProblemContext` holds each hook, and each hook takes a `Models` type.
- The textual estimate gives false positives where a member shares a type's name:
  `MutationContext.Population` read as the type `Models.Population` (found in slice 4).
- The GPU package has no cycles; its `*/Interfaces` directories each hold one
  interface and are nodes of their own. Owner, 2026-10-02: describe them as they are;
  merging them into their parents is a public break left for the GPU redesign.

Slices, in order (bottom-up within each):

1. [x] GPU package: `src/DotNetDifferentialEvolution.GPU` and its 14 subdirectories
   (2026-10-02; 15 nodes, all ✅, linter clean for them).
2. [x] GPU tests: `tests/DotNetDifferentialEvolution.GPU.Test`, `FitnessFunctions`,
   `Helpers` (2026-10-02; 3 nodes, the tests shown non-degenerate by two mutations).
3. [x] CPU leaves: `RandomProviders`, `Helpers`, `ControlParameterProviders`,
   `SelectionStrategies` (+ `Interfaces`) (2026-10-02; 5 nodes). The package node
   `src/DotNetDifferentialEvolution` was started here with package-level facts only,
   because its children link to it for their frame.
   ⚠ 2026-10-02: the slice first also listed `PopulationSamplingMaker` and
   `MutationStrategies` (+ `Interfaces`, `Helpers`). They are not leaves: they use
   `Interfaces` and `Models`, and `MutationStrategies` closes the cycle below through
   `Models` → `MutationStrategies/Interfaces` → its parent. Moved to slice 4; found when
   the slice was read, before anything was written for them.
   ⚠ 2026-10-02, slice 4: half of that note was wrong. `PopulationSamplingMaker` does
   use `Interfaces`, but `MutationStrategies` uses no `Models` type: the estimate
   matched the property `MutationContext.Population` against the type
   `Models.Population`. `MutationStrategies` depends only on `RandomProviders` and is
   not in the cycle; it stayed in slice 4, which changed nothing but the order.
4. [x] CPU core with the cycle: `Models` (+ `Interfaces`), `Interfaces`,
   `GenerationStrategies`, `LocalSearch`, `TerminationStrategies` (+ `Interfaces`),
   `MutationStrategies` (+ `Interfaces`, `Helpers`), `PopulationSamplingMaker`
   (2026-10-02; 11 nodes).
5. [x] CPU engine: `AlgorithmExecutors` (+ `Interfaces`), `Controllers` (and its two
   nested levels), `Algorithms/Common`, `Jde`, `Jade`, `Shade`, `Lshade`, `Variants`,
   then the synthesis of `src/DotNetDifferentialEvolution` itself (2026-10-02; 11 new
   nodes and the package node rewritten; `src/` linter-clean). `Algorithms/` holds no
   code of its own and is not a node: its four children take the package as parent.
6. [x] CPU test support: `tests/DotNetDifferentialEvolution.Tests.Shared` and its
   subdirectories (2026-10-02; 5 nodes). Found for later slices: the benchmark project
   keeps its own copies of Ackley and Rastrigin (slice 9); the declared optima of
   Schwefel and Styblinski-Tang are off by up to 8.8e-4, absorbed by tolerances that the
   unit and integration slices should look at (7, 8).
7. [ ] CPU unit tests: `tests/DotNetDifferentialEvolution.UnitTests` and its 13
   subdirectories.
8. [ ] CPU integration tests: `tests/DotNetDifferentialEvolution.IntegrationTests` and
   its 3 subdirectories.
9. [ ] Benchmarks: `benchmarks/DotNetDifferentialEvolution.Benchmark` and its 3
   subdirectories.
10. [ ] Root: `## Decomposition`, root `API.md`, removal of this section.

`tools/protocol-lint` came with the kit and already carries its pair.
