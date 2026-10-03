# API.md — DotNetDifferentialEvolution.Protocol.Tests

A test node: it exposes nothing to other nodes. Its public classes are xunit test
classes, found by the test runner, not called by code. Its contract points upward: what
the tree may consider machine-checked about its documents. A claim below is ✅ once its
fact was seen red on a mutation (this node's `BOOT.md`, acceptance criteria).

## What this node guarantees ✅

| Claim | Fact |
|---|---|
| The public surface of every library assembly (CPU, GPU, Tests.Shared, Benchmark) equals `PublicSurface.approved.txt` | `SurfaceTests` |
| Every exported type is named under ✅ in its node's `API.md` | `CoverageTests` |
| Every type's namespace is exactly its node's | `NamespaceTests` |
| Every declaration in a C# block under ✅ exists, type and member | `DeclarationTests` |
| `## Dependencies` equals the real crossings: signatures, generic constraints and IL bodies | `DependencyTests` |
| Every source the compiler read lies in a directory with `BOOT.md` and `API.md` | `CompiledSourceTests` |
| The protocol linter passes with `--strict` | `LintTests` |
| No node under `src/` calls `System.Console` | `ForbiddenCallTests` |
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
