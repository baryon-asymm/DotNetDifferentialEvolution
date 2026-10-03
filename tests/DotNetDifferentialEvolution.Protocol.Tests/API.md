# API.md — DotNetDifferentialEvolution.Protocol.Tests

A test node: it exposes nothing to other nodes. Its public classes are xunit test
classes, found by the test runner, not called by code. Its contract points upward: what
the tree may consider machine-checked about its documents. A claim below is ✅ once its
fact was seen red on a mutation (this node's `BOOT.md`, acceptance criteria).

## What this node guarantees ✅

| Claim | Fact |
|---|---|
| The public surface of every library assembly (CPU, GPU, Tests.Common, Benchmark) equals `PublicSurface.approved.txt` | `SurfaceTests` |
| Every exported type is named under ✅ in its node's `API.md` | `CoverageTests` |
| Every type's namespace is exactly its node's | `NamespaceTests` |
| Every declaration in a C# block under ✅ exists, type and member | `DeclarationTests` |
| `## Dependencies` equals the real crossings: signatures, generic constraints and IL bodies | `DependencyTests` |
| Every source the compiler read lies in a directory with `BOOT.md` and `API.md` | `CompiledSourceTests` |
| The protocol linter passes with `--strict` | `LintTests` |
| No node under `src/` calls `System.Console`; the GPU package never calls `GC.Collect` (GPU check 7a) | `ForbiddenCallTests` |
| In the GPU package only `PopulationTransfers` calls an ILGPU host transfer (GPU check 5a) | `GpuGuardTests.OnlyTheTransferHelperCallsAnIlgpuHostTransfer` |
| The IL reachable from every GPU kernel entry point holds no `throw`, `newarr`, `newobj` of a reference type or `box` (GPU check 8a) | `GpuGuardTests.KernelReachableCodeNeitherThrowsNorAllocatesNorBoxes` |
| GPU kernel-reachable code calls only `Abs`, `Sqrt`, `Exp`, `Log`, `Pow`, `Floor`, `Min`, `Max`, `IsNaN` of `Math` and `Double` (GPU check 8b) | `GpuGuardTests.KernelReachableCodeCallsOnlyTheAllowedMathAndDoubleMembers` |
| No constant left of an ordered floating-point comparison in the GPU package's sources (GPU check 8c) | `GpuGuardTests.GpuSourcesPutNoConstantLeftOfAnOrderedFloatingComparison` |
| No `src` method passes host memory to an ILGPU transfer as a raw `ref T` (GPU check 8d) | `GpuGuardTests.NoSrcMethodPassesHostMemoryToAnIlgpuTransferByReference` |
| Nothing in the tree suppresses a diagnostic: no `#pragma warning disable`, `#nullable disable`, suppression attribute, `NoWarn` beyond 1701/1702, `WarningsNotAsErrors`, rule set or severity below warning; no generated-code marker on authored code; no skipped test or hidden theory data; the root `Directory.Build.props` keeps the maximum (`TreatWarningsAsErrors`, `CodeAnalysisTreatWarningsAsErrors`, `EnableNETAnalyzers`, `AnalysisLevel` latest-all, `EnforceCodeStyleInBuild`, `WarningLevel` 9999, `Features` strict, `GenerateDocumentationFile`) | `NoSuppressionGuardTests` |
| Every node path named in `ProtocolConfig` is a node; no library type in the global namespace | `ConfigTests` |

The self-checks of the parsers (`ApiDeclarationsTests`, the helper cases in
`CompiledSourceTests` and `ForbiddenCallTests`) guard the facts and claim nothing about
the tree.

## Running

```bash
dotnet test tests/DotNetDifferentialEvolution.Protocol.Tests -c Release
```

A failing `SurfaceTests` writes `PublicSurface.actual.txt` beside the snapshot. If the
change is intended, update the node's `API.md`, then replace the snapshot with that file
in the same commit.

## Out of scope

- File-level checks (sections, links, marks, sizes): `tools/protocol-lint`, which
  `LintTests` only starts.
- Analyzer-suppression policy, coupling limits, a tree-contract snapshot: kit facts not
  adopted (`BOOT.md`, `## Deviations from the kit`).
