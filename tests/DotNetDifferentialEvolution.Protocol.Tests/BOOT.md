# BOOT.md — DotNetDifferentialEvolution.Protocol.Tests

## Purpose

The reflection half of the protocol's machine checks (AGENTS.md §13): the facts that
compare the tree's documents with the compiled code, which a file-level linter cannot
see. It also starts the linter (`tools/protocol-lint`) in strict mode, so a single
`dotnet test` covers both halves.

Installed 2026-10-03 from the kit `reference/dotnet/` of the `boot-api-protocol` skill
(copied, not linked: the tree root is found by walking up from each source's
`[CallerFilePath]` to `AGENTS.md`, and a linked file outside the tree would not find it).

## Invariants

- **Every fact refuses an empty walk.** A fact that found nothing to check fails with
  "found nothing" instead of passing.
- **The node uses no type of the tree.** It references every node project only so that
  each assembly lands in the build output and loads by name; it reads them by
  reflection, IL and PDB.
- **`PublicSurface.approved.txt` changes only together with the `API.md` it mirrors.**
  The snapshot is a tripwire. The contract is each node's `API.md`.

## Dependencies

None.

Outside the tree: xUnit 2.9.3, Microsoft.NET.Test.Sdk 17.14.1, xunit.runner.visualstudio
3.1.4; Python 3.8+ on `PATH` as `python` (the lint fact); the kit `reference/dotnet/` of
the `boot-api-protocol` skill, as of 2026-10-02.

## Constraints

- Inherited from the root ([BOOT.md](../../BOOT.md)).
- The tests run on the source checkout they were built from: `[CallerFilePath]` must
  point into the tree.
- Uncategorized: they run in the CI gate `Category!=Slow&Category!=Gpu`, not in
  `Category=Unit`.
- ⚠ In CI the CPU package is built with `ContinuousIntegrationBuild`, which maps its PDB
  document paths to `/_/`. `CompiledSources` skips paths that are not absolute, so in CI
  `CompiledSourceTests` does not see the CPU package's sources; a local run does. The
  other assemblies keep real paths, so the fact never runs empty in CI.

## Deviations from the kit

Each one is marked in the code at the place it changes.

- ⚠ **`ProtocolConfig.RootNamespace` is empty.** The tree's top directories carry full
  dotted names (`src/DotNetDifferentialEvolution.GPU` is the namespace
  `DotNetDifferentialEvolution.GPU`), so there is no common prefix to add. `Node.Namespace`
  skips the empty part. `ConfigTests.TheRootNamespaceIsTheTreesOwn` gets an empty-root
  branch instead: no library type may sit in the global namespace. Compiler-made names
  (`<…>`) and a top-level-statements `Program` (it has a `<Main>$` method; the benchmark
  has one) are excluded.
- ⚠ **`CompiledSources` also reads an embedded PDB.** The CPU package sets
  `DebugType embedded` for its package, so no `.pdb` file sits beside its DLL. The kit
  reads only that file.
- ⚠ **`DependencyTests` skips the compiler's namespaceless helpers.**
  `<PrivateImplementationDetails>` (with its `__StaticArrayInitTypeSize=N`) and
  `<>z__ReadOnlyArray<T>` fall to the assembly's project node. For a subnode of a test
  project, that node is its ancestor, so the kit reported a dependency that nobody
  wrote. On the first run that gave 13 of the 48 findings.
- ⚠ **`TypeShape.Shape` reads generic constraints.** `where TRandomGenerator : struct,
  IRandomGenerator` is a real dependency of the GPU kernel controller and of the GPU
  mutation contract. The kit's shape walk missed it and reported the documented link as
  unused. These two findings were false; the code was changed, not the documents.
- ⚠ **The `ProtocolChecks` namespace exception was lifted on 2026-10-03.** It stood as the
  kit's own §1 deviation (`ProtocolConfig.NamespaceExceptions`): these sources kept the
  kit's namespace, so a future kit version could be diffed and copied in. Under the
  maximum diagnostics IDE0130 requires the folder namespace, so the sources are now in
  `DotNetDifferentialEvolution.Protocol.Tests` and the entry is removed. A future kit
  version is diffed after renaming its namespace.
- ⚠ **The kit's files were adapted to the repository's maximum diagnostics on
  2026-10-03** (namespace, generated regexes, naming, XML documentation): behaviour
  unchanged.
- ⚠ **Omitted kit facts:**
  - `NoSuppressionGuardTests`: it would flag the repository's current analyzer policy,
    which is `WarningsNotAsErrors` for NU1901–NU1904 in `Directory.Build.props` and
    CA5394 and CA1716 relaxed for all of `[*.cs]` in `.editorconfig`. Each relaxation
    has its reason written beside it. Whether to keep them is the owner's decision.
  - `CouplingTests` and `TreeContractSnapshotTests`: both are off by default in the
    kit's configuration, and the tree adopts neither.

## Acceptance criteria

- [x] All facts green on the unmutated tree: 2026-10-03, 46 of 46, `dotnet test
      tests/DotNetDifferentialEvolution.Protocol.Tests -c Release` (local, Windows 11,
      .NET SDK 10.0.112).
- [x] The first run's findings are resolved in the documents, not in the facts. On
      2026-10-03, 48 dependency findings:
      - 13 were the compiler helpers and 2 were generic constraints (the deviations
        above);
      - 31 were real links missing from twelve `BOOT.md`, now added and marked "Added
        2026-10-03";
      - 2 were links that EndToEnd declared but never used, now removed with a ⚠ note.
- [x] Each fact is seen red once, with one mutation at a time in a scratch worktree, then
      discarded (2026-10-03; unmutated control 46/46):

      | Mutation | Red |
      |---|---|
      | `## Generation limit ✅` → `⏳` (GPU TerminationStrategies `API.md`) | `CoverageTests` |
      | a `Reset()` member under ✅ that the type lacks | `DeclarationTests` |
      | Lshade's link to ControlParameterProviders removed | `DependencyTests` |
      | GPU MutationStrategies/Interfaces' constraint-only link removed | `DependencyTests` (proves the constraint deviation) |
      | `IsCompilerHelper` skip removed | `DependencyTests`, on GPU.Test/FitnessFunctions, EndToEnd, Tests.Common/FitnessFunctionEvaluators (proves the helper deviation) |
      | an unused link declared (GPU TerminationStrategies) | `DependencyTests` |
      | `internal` type in `…GPU/Nowhere/`, no documents | `NamespaceTests`, `CompiledSourceTests`, `LintTests` |
      | public `Limit` property on `MaxGenerationStrategy` | `SurfaceTests`, `.actual.txt` written |
      | internal type in the global namespace in the GPU package | `ConfigTests.TheRootNamespaceIsTheTreesOwn` |
      | `## Taboos` renamed in a `BOOT.md` | `LintTests` |
      | `Console.WriteLine` in `MaxGenerationStrategy` | `ForbiddenCallTests` |

      The unmutated benchmark calls `Console` and stays green: the rule is scoped to
      `src/`.
- [x] Green in CI: 2026-10-03, GitHub Actions run 37090681168 on PR #12, commit `4b5d35b`, all steps green (the step "Integration tests (excluding slow and
      GPU)", which runs this node on ubuntu-latest). The job log needs a sign-in and was
      not read, so the per-test count in CI is not recorded.

## Taboos

- No fact kept red or skipped: fixed, or removed with a declared deviation.
- No edit to a kit file without a ⚠ paragraph under `## Deviations from the kit`.
- No snapshot update without reading the diff (`PublicSurface.actual.txt`) and the
  matching `API.md` change in the same commit.
