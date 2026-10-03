# BOOT.md — DotNetDifferentialEvolution.GPU/Random

## Purpose

The device's random numbers: Philox4x32-10, a counter-based generator, and the
conversions from its 32-bit words to the values the DE step consumes. Its own node
because reproducibility across runs and across backends rests on it alone.

## Invariants

- **A draw is a pure function of (seed, individual, generation, draw index).** No state
  lives in device memory; each kernel thread rebuilds its stream from the counter
  `(block, individual, generation, 0)` under the key `(seed, 0)`. Generation 0 is the
  initial sampling. Held by ACCEPTANCE.md, checks 4a and 4b.
- **Integer arithmetic only**, so every backend computes the same words: 32×32→64-bit
  products, XOR and wrapping additions. Held by check 4b (CPU accelerator, CUDA,
  OpenCL).
- **The generator is Philox4x32-10 as published.** Held by check 3a against Random123's
  known-answer vectors.
- **A uniform double is 53 random bits**, the largest value `1 − 2⁻⁵³`. Held by check 3b.
- **An index draw is Lemire's multiply-shift without rejection**, bias below `n / 2³²`,
  the CPU package's bound. Held by check 3c.

## Dependencies

None.

Outside the tree: none beyond .NET.

## Constraints

Inherited from the parent ([BOOT.md](../BOOT.md)). In addition:

- Kernel code: no arrays (`PhiloxBlock` has four named fields), no exceptions, no
  allocation.
- A change in how many words a draw consumes, in the counter layout or in the key changes
  every seeded run: it is a breaking change of the package, listed in its notes.

## Acceptance criteria

→ checks 3a, 3b, 3c, 4a and 4b of the package's [ACCEPTANCE.md](../ACCEPTANCE.md).

## Taboos

- **No per-thread state buffer on the device.** The counter is the state.
- **No floating-point arithmetic inside the generator.** It would make the words
  depend on the backend.
- **No KAT value typed from memory.** The vectors are copied from Random123's
  `tests/kat_vectors`, with the source cited in the test.
