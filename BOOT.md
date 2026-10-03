# BOOT.md — DotNetDifferentialEvolution (tree root)

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

The tree was reconstructed brownfield, bottom-up, in nine slices on 2026-10-02 (commits
`63d3ff1` to `60ebcd0`): it records the repository as it is. Where that differs from
what it should be, the node says so with a ⚠; those are the agenda for design sessions.

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
- **Namespaces follow directory paths.** Held by `NamespaceTests`
  ([Protocol.Tests](tests/DotNetDifferentialEvolution.Protocol.Tests/API.md)) since
  2026-10-03; before that, checked by hand on 2026-10-02.

## Dependencies

None.

Outside the tree: .NET SDK 8 and 10 (CI installs both); `DotNetOptimization.Abstractions`
1.0.0 (CPU package); ILGPU and ILGPU.Algorithms 1.5.1 (GPU package); xUnit 2.5.3,
Microsoft.NET.Test.Sdk 17.8.0, coverlet 6.0.0 (tests; Protocol.Tests: xUnit 2.9.3,
Microsoft.NET.Test.Sdk 17.14.1); BenchmarkDotNet 0.14.0 (benchmarks);
Python 3.8+ (`tools/protocol-lint`).

## Constraints

- Shipping code targets `net8.0` with C# 12 pinned (`LangVersion` 12); test projects
  use `latest` and `RollForward=Major` (`tests/Directory.Build.props`).
- CI (`.github/workflows/ci.yml`) runs on `ubuntu-latest`: documentation references,
  the protocol linter and its self-tests, build, `Category=Unit`, then everything except
  `Category=Slow` and `Category=Gpu` (the reflection checks included), then a
  throw-away pack of the CPU package for the API check. Hosted runners have no
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
- [x] The tree passes `protocol_lint` without errors or warnings: 2026-10-03, 75 nodes,
      `python -X utf8 tools/protocol-lint/protocol_lint.py . --exclude templates`.
- [x] Every test node of the repository was shown red once: 2026-10-02, mutations in
      scratch clones (GPU tests in slice 2, unit tests in slice 7, integration tests in
      slice 8); listed in each test node.
- [ ] The linter and its self-tests run in CI (steps "Protocol lint" and "Protocol
      linter self-tests" in `ci.yml`, added 2026-10-03); a first green run on GitHub
      Actions has not been seen.
- [x] The reflection checks are written for this stack and each is proven
      non-degenerate (AGENTS.md §13): 2026-10-03,
      [Protocol.Tests](tests/DotNetDifferentialEvolution.Protocol.Tests/BOOT.md), 46 of
      46 green, every fact red once on a mutation (listed there). Its first run found
      31 missing and 2 stale `## Dependencies` links in twelve nodes, now corrected.
      ⚠ Corrected 2026-10-02, slice 10: this item said the kit's `reference/dotnet/` was
      empty.
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

75 nodes. `src/`, `tests/` and `benchmarks/` hold no code of their own and are not
nodes; neither is `src/DotNetDifferentialEvolution/Algorithms/`.

| Node | Role | Nodes | Readiness defined by |
|---|---|---|---|
| [DotNetDifferentialEvolution](src/DotNetDifferentialEvolution/API.md) | the CPU package | 28 | UnitTests (U0–U2, surface) and IntegrationTests (I0–I3) |
| [DotNetDifferentialEvolution.GPU](src/DotNetDifferentialEvolution.GPU/API.md) | the GPU package | 15 | GPU.Test (L2 only) |
| [Tests.Shared](tests/DotNetDifferentialEvolution.Tests.Shared/API.md) | CPU test support: benchmark functions, fakes, context helper | 5 | its consumers |
| [UnitTests](tests/DotNetDifferentialEvolution.UnitTests/API.md) | the CPU package part by part | 15 | — |
| [IntegrationTests](tests/DotNetDifferentialEvolution.IntegrationTests/API.md) | the CPU engine as a whole | 4 | — |
| [GPU.Test](tests/DotNetDifferentialEvolution.GPU.Test/API.md) | two end-to-end GPU runs | 3 | — |
| [Benchmark](benchmarks/DotNetDifferentialEvolution.Benchmark/API.md) | throughput and convergence measurement, no assertions | 2 | — |
| [protocol-lint](tools/protocol-lint/API.md) | the tree's file-level checks | 1 | its own tests |
| [Protocol.Tests](tests/DotNetDifferentialEvolution.Protocol.Tests/API.md) | the reflection checks (§13): documents against compiled code | 1 | mutations, once |

Dependencies run one way: test projects and benchmarks depend on a package and on
Tests.Shared; Tests.Shared on the CPU package; the packages on nothing in the tree. The
one cycle is inside the CPU package (`Models` and the hook contracts; see its
`BOOT.md`, `## Decomposition`).

The open findings with the most weight, each recorded in full in its node:

- jDE's tie rule is pinned by a test but has no cited source
  ([Variants](src/DotNetDifferentialEvolution/Variants/BOOT.md)).
- Two documented divergences of L-SHADE from Tanabe's code
  ([Lshade](src/DotNetDifferentialEvolution/Algorithms/Lshade/BOOT.md),
  [Shade](src/DotNetDifferentialEvolution/Algorithms/Shade/BOOT.md)).
- Approximate declared optima for Schwefel and Styblinski-Tang
  ([FitnessFunctionEvaluators](tests/DotNetDifferentialEvolution.Tests.Shared/FitnessFunctionEvaluators/BOOT.md)).
- The GPU package diverges from the CPU package in semantics a user may carry over
  ([GPU](src/DotNetDifferentialEvolution.GPU/BOOT.md)).
