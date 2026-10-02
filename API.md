# API.md — DotNetDifferentialEvolution

Tree root. The repository exposes two NuGet packages: `DotNetDifferentialEvolution`
(CPU) and `DotNetDifferentialEvolution.GPU` (ILGPU). Their outward contracts are
recovered slice by slice (`BOOT.md`, `## Reconstruction`) and generalized here in the
last slice.

## How the system is used ⏳

To be synthesized from the children's `API.md` once they exist. Until then the
consumer-facing description is `README.md` and `docs/AGENT_GUIDE.md` (CPU package) and
`src/DotNetDifferentialEvolution.GPU/README.md` (GPU package).

## Children

- [DotNetDifferentialEvolution.GPU](src/DotNetDifferentialEvolution.GPU/API.md) — the
  GPU package: `DifferentialEvolutionOptimizer` over an ILGPU kernel controller.

The CPU package is added when its slices are described.

## Test nodes

Filled in as each test slice is described.
