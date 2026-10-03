using DotNetDifferentialEvolution.GPU.Kernels;
using DotNetDifferentialEvolution.GPU.Random;
using ILGPU;
using ILGPU.Runtime;

namespace DotNetDifferentialEvolution.GPU.Test.Random;

/// <summary>
/// The three Philox4x32-10 known-answer vectors of Random123 (ACCEPTANCE.md, check 3a), and the
/// launch of <see cref="GpuKernels.PhiloxBlocks"/> over them.
/// </summary>
/// <remarks>
/// Source: D. E. Shaw Research, Random123, <c>tests/kat_vectors</c>, lines 27–29
/// (https://github.com/DEShawResearch/random123, HEAD 9545ff6413f258be2f04c1d319d99aaef7521150;
/// file SHA-256 aab5ebabf40003f63d6d87b24cbd2c8a02652e00cf8bad64226fd50586929183). Each line is
/// <c>philox4x32 10 CTR[4] KEY[2] EXPECTED[4]</c>: the zero vector, the all-ones vector and the
/// digits of π.
/// </remarks>
internal static class PhiloxKatVectors
{
    /// <summary>Gets the counters, four words per vector.</summary>
    public static IReadOnlyList<uint> Counters { get; } =
    [
        0x00000000, 0x00000000, 0x00000000, 0x00000000,
        0xffffffff, 0xffffffff, 0xffffffff, 0xffffffff,
        0x243f6a88, 0x85a308d3, 0x13198a2e, 0x03707344,
    ];

    /// <summary>Gets the keys, two words per vector.</summary>
    public static IReadOnlyList<uint> Keys { get; } =
    [
        0x00000000, 0x00000000,
        0xffffffff, 0xffffffff,
        0xa4093822, 0x299f31d0,
    ];

    /// <summary>Gets the expected blocks, four words per vector.</summary>
    public static IReadOnlyList<uint> Expected { get; } =
    [
        0x6627e8d5, 0xe169c58d, 0xbc57ac4c, 0x9b00dbd8,
        0x408f276d, 0x41c83b0e, 0xa20bc7c6, 0x6d5451fd,
        0xd16cfe09, 0x94fdcceb, 0x5001e420, 0x24126ea1,
    ];

    /// <summary>Gets the number of vectors.</summary>
    public static int Count => Keys.Count / 2;

    /// <summary>Runs <see cref="GpuKernels.PhiloxBlocks"/> over the three vectors on <paramref name="accelerator"/>.</summary>
    /// <param name="accelerator">The accelerator.</param>
    /// <returns>The blocks the kernel wrote, four words per vector.</returns>
    public static uint[] RunKernel(Accelerator accelerator)
    {
        var kernel = accelerator.LoadAutoGroupedStreamKernel<Index1D, ArrayView<uint>, ArrayView<uint>, ArrayView<uint>>(GpuKernels.PhiloxBlocks);
        using var counters = accelerator.Allocate1D<uint>(Counters.Count);
        using var keys = accelerator.Allocate1D<uint>(Keys.Count);
        using var blocks = accelerator.Allocate1D<uint>(Expected.Count);
        counters.View.CopyFromCPU([.. Counters]);
        keys.View.CopyFromCPU([.. Keys]);

        kernel(Count, counters.View, keys.View, blocks.View);
        accelerator.Synchronize();

        var result = new uint[Expected.Count];
        blocks.View.CopyToCPU(result);
        return result;
    }

    /// <summary>The block of vector <paramref name="vector"/> computed on the host.</summary>
    /// <param name="vector">The vector, 0 to 2.</param>
    /// <returns>The four words.</returns>
    public static uint[] OnHost(int vector)
    {
        var block = Philox4x32x10.Generate(
            new PhiloxBlock(Counters[4 * vector], Counters[4 * vector + 1], Counters[4 * vector + 2], Counters[4 * vector + 3]),
            Keys[2 * vector],
            Keys[2 * vector + 1]);
        return [block.X0, block.X1, block.X2, block.X3];
    }

    /// <summary>The expected words of vector <paramref name="vector"/>.</summary>
    /// <param name="vector">The vector, 0 to 2.</param>
    /// <returns>The four words.</returns>
    public static uint[] ExpectedOf(int vector) => [.. Expected.Skip(4 * vector).Take(4)];
}
