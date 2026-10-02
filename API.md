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

- [DotNetDifferentialEvolution](src/DotNetDifferentialEvolution/API.md) — the CPU
  package; being described (slices 3 to 5).
- [DotNetDifferentialEvolution.GPU](src/DotNetDifferentialEvolution.GPU/API.md) — the
  GPU package: `DifferentialEvolutionOptimizer` over an ILGPU kernel controller.

## Test nodes

- [DotNetDifferentialEvolution.GPU.Test](tests/DotNetDifferentialEvolution.GPU.Test/API.md)
  — two end-to-end GPU runs against known optima; local only (needs OpenCL).

The CPU package's test nodes are added when their slices are described.
