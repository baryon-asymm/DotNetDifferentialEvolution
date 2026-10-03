# BOOT.md — DotNetDifferentialEvolution (tree root)

## Purpose

The repository builds two NuGet packages of Differential Evolution (DE) for .NET:

- `DotNetDifferentialEvolution` (`src/DotNetDifferentialEvolution`, version 6.0.0 in
  its csproj, 5.1.0 the latest on nuget.org): CPU, multi-threaded, SIMD; classic DE plus
  jDE, JADE, SHADE and L-SHADE.
- `DotNetDifferentialEvolution.GPU` (`src/DotNetDifferentialEvolution.GPU`, version
  1.0.0 in its csproj, 0.2.0 the latest on nuget.org): DE/rand/1/bin on a GPU through
  ILGPU. Imported into this repository with its history on 2026-10-02 (merge `fe13623`)
  from the separate repository `baryon-asymm/DotNetDifferentialEvolution.GPU`, now
  archived; rebuilt as 1.0.0 on 2026-10-03.

The two packages are siblings: neither references the other. Both depend on the shared
`DotNetOptimization.Abstractions` package for the solution type, and the CPU package for
the objective contract too.

Not goals: asynchronous or GPU evaluation in the CPU package (`docs/AGENT_GUIDE.md`,
"What it does not do"); constraints beyond box bounds; integer or variable-length
genomes; multi-objective optimization.

The tree was reconstructed brownfield, bottom-up, in nine slices on 2026-10-02 (commits
`63d3ff1` to `60ebcd0`): it records the repository as it is. Where that differs from
what it should be, the node says so with a ⚠; those are the agenda for design sessions.

## Invariants

- **Every build is at the compiler's and the analyzers' maximum, and every diagnostic
  is an error, with nothing suppressed anywhere** (owner's decision, 2026-10-03). The
  analyzers at `latest-all`, code style enforced, `WarningLevel` 9999, `Features=strict`,
  XML documentation for every project, and in `.editorconfig` every analyzer diagnostic
  at least a warning. No `#pragma`, no `SuppressMessage`, no `NoWarn` (the SDK's default
  list is cleared in `Directory.Build.targets`), no `WarningsNotAsErrors` (the NuGet
  vulnerability audit NU1901–NU1904 included), no rule lowered in `.editorconfig`. Held by
  `Directory.Build.props`, `Directory.Build.targets` and `.editorconfig`, which every
  project inherits; tests and benchmarks are not exempt.
- **The CPU package does not depend on ILGPU or on the GPU package.** Held by the
  project references of `src/DotNetDifferentialEvolution`; the owner's decision of
  2026-10-02, recorded at the import.
- **The CPU package's public surface is diffed against its last release on every CI
  run.** Held by package validation (`EnablePackageValidation`, baseline 5.1.0) and the
  CI "Pack" step; deliberate breaks are listed in `CompatibilitySuppressions.xml`.
- **Namespaces follow directory paths.** Held by `NamespaceTests`
  ([Protocol.Tests](tests/DotNetDifferentialEvolution.Protocol.Tests/API.md)) since
  2026-10-03; before that, checked by hand on 2026-10-02.

## Dependencies

None.

Outside the tree: .NET SDK 8 and 10 (CI installs both); `DotNetOptimization.Abstractions`
1.0.0 (CPU package); ILGPU 1.5.3 (GPU package); xUnit 2.9.3, xunit.runner.visualstudio
3.1.4, Microsoft.NET.Test.Sdk 17.14.1, coverlet 6.0.0 (every test project, since 2026-10-03:
the older versions trip CA1515 on public test classes); BenchmarkDotNet 0.14.0 (benchmarks);
Python 3.8+ (`tools/protocol-lint`).

## Constraints

- Shipping code targets `net8.0` with C# 12 pinned (`LangVersion` 12); test projects
  use `latest` and `RollForward=Major` (`tests/Directory.Build.props`).
- CI (`.github/workflows/ci.yml`) runs on `ubuntu-latest`: documentation references,
  the protocol linter and its self-tests, build, `Category=Unit`, then everything except
  `Category=Slow` and `Category=Gpu` (the reflection checks and the GPU package's suite
  on ILGPU's CPU accelerator included), then a throw-away pack of each package, the
  CPU one with the API check. Hosted runners have no GPU: the `Category=Gpu` tests run
  on a developer machine with CUDA and OpenCL.
- Releases: `.github/workflows/release.yml` publishes the CPU package from a `v*` tag
  and the GPU package from a `gpu-v*` tag; a tag never publishes the other package.
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
- [x] The linter and its self-tests run in CI (steps "Protocol lint" and "Protocol
      linter self-tests" in `ci.yml`): 2026-10-03, GitHub Actions run 37090681168 on PR #12, commit `4b5d35b`, all steps green.
- [x] The reflection checks are written for this stack and each is proven
      non-degenerate (AGENTS.md §13): 2026-10-03,
      [Protocol.Tests](tests/DotNetDifferentialEvolution.Protocol.Tests/BOOT.md), 46 of
      46 green, every fact red once on a mutation (listed there). Its first run found
      31 missing and 2 stale `## Dependencies` links in twelve nodes, now corrected.
      ⚠ Corrected 2026-10-02, slice 10: this item said the kit's `reference/dotnet/` was
      empty.
- [x] ⚠ The GPU package had no release path (`release.yml` packed only the CPU package
      and both would have shared the `v*` tags). Closed 2026-10-03: a `publish-gpu` job
      on `gpu-v*` tags, and CI packs the GPU package without publishing.
- [x] ⚠ The GPU package's csproj said 0.0.2 while nuget.org carries 0.1.0, 0.0.2 and
      0.2.0, 0.2.0 published from a version never committed to git. Closed 2026-10-03:
      the csproj says 1.0.0, the next version to publish.
- [x] ⚠ The GPU tests ran in no CI: hosted runners have no OpenCL device. Closed
      2026-10-03: the GPU suite runs on ILGPU's CPU accelerator in the CI gate; only
      `Category=Gpu` stays local.

## Taboos

- **No ILGPU (or any GPU runtime) in the CPU package.** Every CPU consumer would carry
  a native GPU runtime it does not use (owner, 2026-10-02).
- **No `GeneratePackageOnBuild`.** A package built from a local branch carries a
  SourceLink map to a commit that may never be pushed; packages are packed only from a
  tagged commit (`ecd8f09`, 2026-07-28; the CPU csproj explains it).
- **No suppression of any diagnostic, anywhere.** No `#pragma`, `SuppressMessage`,
  `NoWarn`, `WarningsNotAsErrors`, `severity = none`, `#nullable disable`, skipped test
  or hidden theory data. A diagnostic is resolved in code; one that code cannot resolve
  goes to the owner (2026-10-03).
- **No push, tag, publish or merge into `main` without the owner's word**, each time.

## Decomposition

75 nodes. `src/`, `tests/` and `benchmarks/` hold no code of their own and are not
nodes; neither is `src/DotNetDifferentialEvolution/Algorithms/`.

| Node | Role | Nodes | Readiness defined by |
|---|---|---|---|
| [DotNetDifferentialEvolution](src/DotNetDifferentialEvolution/API.md) | the CPU package | 28 | UnitTests (U0–U2, surface) and IntegrationTests (I0–I3) |
| [DotNetDifferentialEvolution.GPU](src/DotNetDifferentialEvolution.GPU/API.md) | the GPU package | 15 | GPU.Test (L2 only) |
| [Tests.Common](tests/DotNetDifferentialEvolution.Tests.Common/API.md) | CPU test support: benchmark functions, fakes, context helper | 5 | its consumers |
| [UnitTests](tests/DotNetDifferentialEvolution.UnitTests/API.md) | the CPU package part by part | 15 | — |
| [IntegrationTests](tests/DotNetDifferentialEvolution.IntegrationTests/API.md) | the CPU engine as a whole | 4 | — |
| [GPU.Test](tests/DotNetDifferentialEvolution.GPU.Test/API.md) | two end-to-end GPU runs | 3 | — |
| [Benchmark](benchmarks/DotNetDifferentialEvolution.Benchmark/API.md) | throughput and convergence measurement, no assertions | 2 | — |
| [protocol-lint](tools/protocol-lint/API.md) | the tree's file-level checks | 1 | its own tests |
| [Protocol.Tests](tests/DotNetDifferentialEvolution.Protocol.Tests/API.md) | the reflection checks (§13): documents against compiled code | 1 | mutations, once |

Dependencies run one way: test projects and benchmarks depend on a package and on
Tests.Common; Tests.Common on the CPU package; the packages on nothing in the tree. The
one cycle is inside the CPU package (`Models` and the hook contracts; see its
`BOOT.md`, `## Decomposition`).

The open findings with the most weight, each recorded in full in its node:

- jDE's tie rule is pinned by a test but has no cited source
  ([Variants](src/DotNetDifferentialEvolution/Variants/BOOT.md)).
- Two documented divergences of L-SHADE from Tanabe's code
  ([Lshade](src/DotNetDifferentialEvolution/Algorithms/Lshade/BOOT.md),
  [Shade](src/DotNetDifferentialEvolution/Algorithms/Shade/BOOT.md)).
- Approximate declared optima for Schwefel and Styblinski-Tang
  ([FitnessFunctionEvaluators](tests/DotNetDifferentialEvolution.Tests.Common/FitnessFunctionEvaluators/BOOT.md)).
- The GPU package's CUDA math (`Exp`, `Log`, `Pow` through ILGPU.Algorithms) against
  APT's 4-ULP bound, check D2 of its
  [ACCEPTANCE.md](src/DotNetDifferentialEvolution.GPU/ACCEPTANCE.md).
