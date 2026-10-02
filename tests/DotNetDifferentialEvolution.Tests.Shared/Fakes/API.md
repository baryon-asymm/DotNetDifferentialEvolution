# API.md — Tests.Shared/Fakes

Namespace: `DotNetDifferentialEvolution.Tests.Shared.Fakes`. Random providers that make
the package's stochastic code assertable.

## Providers ✅

```csharp
public sealed class ScriptedRandomProvider : BaseRandomProvider
{
    public ScriptedRandomProvider(IEnumerable<int>? ints = null, IEnumerable<double>? doubles = null);
    public bool CycleWhenExhausted { get; init; }   // default false
    public int IntDrawCount { get; }
    public int DoubleDrawCount { get; }
    public override int Next(int maxValue);
    public override double NextDouble();
}

public sealed class DeterministicRandomProvider : BaseRandomProvider
{
    public DeterministicRandomProvider(int seed = 0);   // System.Random(seed)
    public override int Next(int maxValue);
    public override double NextDouble();
}
```

`ScriptedRandomProvider` replays its two queues independently, in order. The counters
say how many draws each queue has served, so a test can assert how many draws the code
made.

## Errors

| Situation | Behaviour (`ScriptedRandomProvider`) |
|---|---|
| A queue is empty and its method is called | `InvalidOperationException` |
| A queue is exhausted, `CycleWhenExhausted` is false | `InvalidOperationException` |
| A scripted `Next` value outside `[0, maxValue)` | `InvalidOperationException` naming the value and the range |

## Side effects

None beyond the providers' own cursors. Neither type is thread-safe.
