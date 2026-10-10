using System.Runtime.InteropServices;
using DotNetDifferentialEvolution.GPU.Objectives;

namespace DotNetDifferentialEvolution.GPU.Test.Builder;

/// <summary>
/// A pointwise objective of any point type whose methods are never run: the point-type cases (check A5) only start a
/// run with <c>ForPointwiseFunction</c>, which refuses or accepts the type before anything is built.
/// </summary>
/// <typeparam name="TPoint">The point type under test.</typeparam>
internal readonly struct PointProbe<TPoint> : IGpuPointwiseFitnessFunction<TPoint>
    where TPoint : unmanaged
{
    /// <inheritdoc />
    public TPoint EvaluatePoint(GeneView genes, int point) => default;

    /// <inheritdoc />
    public double Combine(GeneView genes, PointView<TPoint> points) => 0.0;
}

/// <summary>A verdict of one point, an enum of <see cref="int"/>.</summary>
internal enum Verdict
{
    /// <summary>Within tolerance.</summary>
    Low = 0,

    /// <summary>Beyond tolerance.</summary>
    High = 1,
}

/// <summary>An enum of <see cref="long"/>.</summary>
internal enum WideVerdict : long
{
    /// <summary>Within tolerance.</summary>
    Low = 0,

    /// <summary>Beyond tolerance.</summary>
    High = 1,
}

/// <summary>The shape of check P1's point result: a <see cref="double"/> and an <see cref="int"/>.</summary>
/// <param name="Residual">The residual.</param>
/// <param name="Flag">The flag.</param>
internal readonly record struct DoubleAndInt(double Residual, int Flag);

/// <summary>The shape of check P3's point result: two <see cref="double"/>s and an <see cref="int"/>.</summary>
/// <param name="First">The first value.</param>
/// <param name="Second">The second value.</param>
/// <param name="Count">The count.</param>
internal readonly record struct TwoDoublesAndInt(double First, double Second, int Count);

/// <summary>A struct nesting two <see cref="DoubleAndInt"/>s.</summary>
/// <param name="Left">The first.</param>
/// <param name="Right">The second.</param>
internal readonly record struct NestingTwo(DoubleAndInt Left, DoubleAndInt Right);

/// <summary>A struct with an <see cref="int"/> enum and a <see cref="double"/>.</summary>
/// <param name="Verdict">The verdict.</param>
/// <param name="Value">The value.</param>
internal readonly record struct WithIntEnum(Verdict Verdict, double Value);

/// <summary>A struct of a <see cref="byte"/>, a <see cref="short"/> and a <see cref="byte"/>, padded to 6 bytes.</summary>
/// <param name="First">The first byte.</param>
/// <param name="Middle">The short.</param>
/// <param name="Last">The last byte.</param>
internal readonly record struct SmallFields(byte First, short Middle, byte Last);

/// <summary>A struct of a <see cref="WideVerdict"/> and a <see cref="float"/>.</summary>
/// <param name="Verdict">The verdict.</param>
/// <param name="Ratio">The ratio.</param>
internal readonly record struct WithWideEnum(WideVerdict Verdict, float Ratio);

/// <summary>A <see cref="bool"/> field is not blittable.</summary>
/// <param name="Value">The value.</param>
/// <param name="Flag">The flag.</param>
internal readonly record struct WithBool(double Value, bool Flag);

/// <summary>A <see cref="char"/> field is not blittable.</summary>
/// <param name="Value">The value.</param>
/// <param name="Letter">The letter.</param>
internal readonly record struct WithChar(double Value, char Letter);

/// <summary>A native integer field has no fixed size.</summary>
/// <param name="Value">The value.</param>
/// <param name="Handle">The native integer.</param>
internal readonly record struct WithNativeInt(double Value, nint Handle);

/// <summary>A struct of a <see cref="byte"/> and a <see cref="double"/> packed to 9 bytes, which the device strides by 16.</summary>
/// <param name="Tag">The byte.</param>
/// <param name="Value">The double.</param>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
internal readonly record struct PackedByteDouble(byte Tag, double Value);

/// <summary>A struct of an <see cref="int"/> and a <see cref="double"/> packed to 12 bytes, which the device strides by 16.</summary>
/// <param name="Tag">The int.</param>
/// <param name="Value">The double.</param>
[StructLayout(LayoutKind.Sequential, Pack = 4)]
internal readonly record struct PackedIntDouble(int Tag, double Value);

/// <summary>A sequential struct padded by <c>Size</c> beyond its natural 16 bytes.</summary>
/// <param name="Tag">The int.</param>
/// <param name="Value">The double.</param>
[StructLayout(LayoutKind.Sequential, Size = 24)]
internal readonly record struct SizedPair(int Tag, double Value);

/// <summary>A struct of automatic layout, whose field offsets the runtime picks.</summary>
/// <param name="Tag">The int.</param>
/// <param name="Value">The double.</param>
[StructLayout(LayoutKind.Auto)]
internal readonly record struct AutoPair(int Tag, double Value);

/// <summary>A struct of explicit layout, here the natural one.</summary>
/// <param name="Tag">The int.</param>
/// <param name="Value">The double.</param>
[StructLayout(LayoutKind.Explicit)]
internal readonly record struct ExplicitPair([field: FieldOffset(0)] int Tag, [field: FieldOffset(8)] double Value);

/// <summary>A struct nesting a refused one two levels down: <c>Inner.Deep.Flag</c>.</summary>
/// <param name="Count">The count.</param>
/// <param name="Inner">The nested struct.</param>
internal readonly record struct NestingBool(int Count, NestedOnce Inner);

/// <summary>The middle level of <see cref="NestingBool"/>.</summary>
/// <param name="Weight">The weight.</param>
/// <param name="Deep">The struct with the <see cref="bool"/>.</param>
internal readonly record struct NestedOnce(double Weight, WithBool Deep);

/// <summary>A struct nesting a packed one.</summary>
/// <param name="Count">The count.</param>
/// <param name="Inner">The packed struct.</param>
internal readonly record struct NestingPacked(int Count, PackedByteDouble Inner);
