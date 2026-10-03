# API.md — DotNetDifferentialEvolution.GPU/Random

Namespace: `DotNetDifferentialEvolution.GPU.Random`. Internal to the assembly; visible to
the GPU test project and to ILGPU's runtime assembly. Consumed by
[Kernels](../Kernels/API.md).

## Internal to the assembly ✅

```csharp
internal readonly record struct PhiloxBlock(uint X0, uint X1, uint X2, uint X3)
{
    public uint Word(int position);
}

internal static class Philox4x32x10
{
    public const uint Multiplier0 = 0xD2511F53;
    public const uint Multiplier1 = 0xCD9E8D57;
    public const uint Weyl0 = 0x9E3779B9;
    public const uint Weyl1 = 0xBB67AE85;
    public const int Rounds = 10;

    public static PhiloxBlock Generate(PhiloxBlock counter, uint key0, uint key1);
}

internal interface IDrawSource
{
    uint NextUInt();
    int NextIndex(int n);
    ulong NextULong();
    double NextUnitDouble();
}

internal struct PhiloxDraws : IDrawSource
{
    public PhiloxDraws(int seed, int individual, int generation);
}

internal static class DrawConversions
{
    public const double UnitDoubleScale = 1.0 / 9007199254740992.0;

    public static int ToIndex(uint word, int n);
    public static double ToUnitDouble(uint high, uint low);
    public static ulong ToULong(uint high, uint low);
}
```

- `Generate` is Random123's `philox4x32` with 10 rounds: the round
  `(hi(M1·c2) ⊕ c1 ⊕ k0, lo(M1·c2), hi(M0·c0) ⊕ c3 ⊕ k1, lo(M0·c0))`, the key bumped by
  the Weyl constants between rounds.
- `PhiloxDraws(seed, i, g)` yields the words of blocks 0, 1, 2… at counter
  `(block, i, g, 0)` under key `(seed, 0)`, in order X0..X3.
- Word use per draw: `NextUInt` one; `NextIndex(n)` one, `⌊w·n / 2³²⌋`; `NextULong` two,
  the first the high half; `NextUnitDouble` two, `(w₁ >> 5)·2²⁶ + (w₂ >> 6)` times 2⁻⁵³.
- `IDrawSource` is implemented by structs and taken as a generic argument by reference,
  so a test can script the draws the DE step consumes.
