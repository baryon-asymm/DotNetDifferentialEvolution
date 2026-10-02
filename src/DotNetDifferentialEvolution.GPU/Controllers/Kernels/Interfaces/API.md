# API.md — GPU kernel controller contract

Namespace: `DotNetDifferentialEvolution.GPU.Controllers.Kernels.Interfaces`. The
lifecycle of a GPU run as the optimizer drives it. Everything not listed here is
internal structure and may change.

## Contract ✅

```csharp
public interface IKernelController : IDisposable
{
    void CompileAndGpuMemoryAlloc();
    void Init();
    void Run();
    void Run(CancellationToken cancellationToken);
    HostPopulation? GetCurrentPopulationOrNull();
}
```

The intended order, stated in the XML documentation of the interface:

1. `CompileAndGpuMemoryAlloc()` once — compile the kernels, allocate the populations;
   a second call throws `InvalidOperationException`.
2. `Init()` — evaluate the fitness of the current population on the device.
3. `Run()` / `Run(cancellationToken)` — generations until the termination rule or the
   token stops them; returns normally in both cases.
4. `GetCurrentPopulationOrNull()` — the current population (device buffers), `null`
   before step 1.
5. `Dispose()` — free what the controller allocated.

All calls are synchronous and block the calling thread.

## Errors

| Situation | Behaviour |
|---|---|
| `Init` or `Run` before `CompileAndGpuMemoryAlloc` | `InvalidOperationException` |
| `CompileAndGpuMemoryAlloc` twice | `InvalidOperationException` |
| `Run` without `Init` | Not prevented by the contract; see the implementation |

## Side effects

Device memory allocation and kernel launches; see the implementation.

## Out of scope

- Building the result: the optimizer reads the population and picks the best.
