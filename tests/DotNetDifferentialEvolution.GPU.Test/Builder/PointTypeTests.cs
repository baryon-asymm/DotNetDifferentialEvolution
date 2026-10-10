namespace DotNetDifferentialEvolution.GPU.Test.Builder;

/// <summary>
/// Check A5 of the GPU package's Kernels ACCEPTANCE.md: <c>ForPointwiseFunction</c> accepts a point type of sequential
/// layout, with fields of the numeric primitives, their enums or such structs, and of its natural size, and refuses any other
/// with an <see cref="ArgumentException"/> whose <c>ParamName</c> is <c>TPoint</c> and whose message names the type and the
/// field, nested fields by their path. Each type has its own case. The kernels write the results at the natural stride; a
/// packed type has a smaller size on the host, and the kernel wrote out of bounds (1 667 times on CUDA at <c>c40868e</c>).
/// </summary>
public class PointTypeTests
{
    /// <summary>A <see cref="double"/> is accepted.</summary>
    [Fact]
    public void ADoubleIsAccepted() => AssertAccepted<double>();

    /// <summary>An <see cref="int"/> is accepted.</summary>
    [Fact]
    public void AnIntIsAccepted() => AssertAccepted<int>();

    /// <summary>An enum of <see cref="int"/> is accepted.</summary>
    [Fact]
    public void AnIntEnumIsAccepted() => AssertAccepted<Verdict>();

    /// <summary>A record struct of a <see cref="double"/> and an <see cref="int"/>, the point type of check P1, is accepted.</summary>
    [Fact]
    public void ADoubleAndAnIntAreAccepted() => AssertAccepted<DoubleAndInt>();

    /// <summary>Two <see cref="double"/>s and an <see cref="int"/>, the shape of the point types of checks P3 and P4, are accepted.</summary>
    [Fact]
    public void TwoDoublesAndAnIntAreAccepted() => AssertAccepted<TwoDoublesAndInt>();

    /// <summary>A struct nesting two accepted structs is accepted.</summary>
    [Fact]
    public void AStructNestingTwoAcceptedStructsIsAccepted() => AssertAccepted<NestingTwo>();

    /// <summary>A struct with an <see cref="int"/> enum is accepted.</summary>
    [Fact]
    public void AStructWithAnIntEnumIsAccepted() => AssertAccepted<WithIntEnum>();

    /// <summary>A struct with an enum of <see cref="long"/> and a <see cref="float"/> is accepted.</summary>
    [Fact]
    public void AStructWithALongEnumAndAFloatIsAccepted() => AssertAccepted<WithWideEnum>();

    /// <summary>A struct of two bytes around a short, padded to 6 bytes, is accepted: the padding is the natural one.</summary>
    [Fact]
    public void AStructOfSmallFieldsWithNaturalPaddingIsAccepted() => AssertAccepted<SmallFields>();

    /// <summary>A <see cref="bool"/> field is refused, and the message names the type and the field.</summary>
    [Fact]
    public void ABoolFieldIsRefused() => AssertRefused<WithBool>("'Flag'", "System.Boolean");

    /// <summary>A <see cref="char"/> field is refused, and the message names the type and the field.</summary>
    [Fact]
    public void ACharFieldIsRefused() => AssertRefused<WithChar>("'Letter'", "System.Char");

    /// <summary>A native integer field, which has no fixed size, is refused.</summary>
    [Fact]
    public void ANativeIntFieldIsRefused() => AssertRefused<WithNativeInt>("'Handle'", "System.IntPtr");

    /// <summary>A point type that is a <see cref="bool"/> itself is refused.</summary>
    [Fact]
    public void ABoolIsRefused() => AssertRefused<bool>("System.Boolean");

    /// <summary>A byte followed by a double with <c>Pack = 1</c>, 9 bytes on the host and 16 on the device, is refused.</summary>
    [Fact]
    public void APackOfOneByteAndDoubleIsRefused() => AssertRefused<PackedByteDouble>("'Value'", "9 bytes", "16");

    /// <summary>An int followed by a double with <c>Pack = 4</c>, 12 bytes on the host and 16 on the device, is refused.</summary>
    [Fact]
    public void APackOfFourIntAndDoubleIsRefused() => AssertRefused<PackedIntDouble>("'Value'", "12 bytes", "16");

    /// <summary>A sequential struct padded by <c>Size</c> beyond its natural size is refused.</summary>
    [Fact]
    public void AStructPaddedBySizeIsRefused() => AssertRefused<SizedPair>("24 bytes", "16");

    /// <summary>A struct of <c>LayoutKind.Auto</c> is refused.</summary>
    [Fact]
    public void AutoLayoutIsRefused() => AssertRefused<AutoPair>("Auto layout");

    /// <summary>A struct of <c>LayoutKind.Explicit</c> is refused, even where its offsets are the natural ones.</summary>
    [Fact]
    public void ExplicitLayoutIsRefused() => AssertRefused<ExplicitPair>("Explicit layout");

    /// <summary>A struct nesting a refused one is refused, and the message gives the path to the field two levels down.</summary>
    [Fact]
    public void AStructNestingARefusedOneIsRefusedByThePathToTheField() => AssertRefused<NestingBool>("'Inner.Deep.Flag'", "System.Boolean");

    /// <summary>A struct nesting a packed one is refused, and the message gives the path to the misplaced field.</summary>
    [Fact]
    public void AStructNestingAPackedOneIsRefusedByThePathToTheField() => AssertRefused<NestingPacked>("'Inner.Value'", "9 bytes");

    private static void AssertAccepted<TPoint>()
        where TPoint : unmanaged =>
        Assert.NotNull(Start<TPoint>());

    private static void AssertRefused<TPoint>(params string[] fragments)
        where TPoint : unmanaged
    {
        var failure = Assert.Throws<ArgumentException>(Start<TPoint>);

        Assert.Equal("TPoint", failure.ParamName);
        Assert.Contains(typeof(TPoint).FullName!, failure.Message, StringComparison.Ordinal);
        foreach (var fragment in fragments)
        {
            Assert.Contains(fragment, failure.Message, StringComparison.Ordinal);
        }
    }

    private static IGpuBoundsRequired<PointProbe<TPoint>> Start<TPoint>()
        where TPoint : unmanaged =>
        GpuDifferentialEvolutionBuilder.ForPointwiseFunction<PointProbe<TPoint>, TPoint>(default, 1);
}
