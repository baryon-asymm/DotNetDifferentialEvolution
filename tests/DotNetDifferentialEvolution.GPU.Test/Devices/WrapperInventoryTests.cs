using DotNetDifferentialEvolution.GPU.Devices.LibDevice;

namespace DotNetDifferentialEvolution.GPU.Test.Devices;

/// <summary>
/// Check L2 of the GPU package's ACCEPTANCE.md: the post-link's wrapper inventory over three committed PTX texts of
/// <c>MathProbe.Probe</c> (provenance in this node's BOOT.md): ILGPU 1.5.3's own for the RTX 5070 Ti (<c>SM_120</c>),
/// which calls the wrappers and defines none; the same text after the post-link, which defines every wrapper it calls;
/// and ILGPU's own for <c>SM_89</c>, an architecture below compute 10.0, where ILGPU defines them itself. No fact names a
/// wrapper: each asserts a relation the texts must stand in. After APThermo's <c>WrapperInventoryTests</c>
/// (commit <c>5fdd82c</c>).
/// </summary>
public class WrapperInventoryTests
{
    /// <summary>ILGPU's own PTX calls wrappers and defines none of them.</summary>
    [Fact]
    public void IlgpusOwnPtxForSm120CallsWrappersAndDefinesNone()
    {
        var ptx = PtxFixtures.Native();

        Assert.NotEmpty(LibDevicePostLink.WrappersCalled(ptx));
        Assert.Empty(LibDevicePostLink.WrappersDefined(ptx));
    }

    /// <summary>After the post-link, the PTX defines exactly the wrappers it calls.</summary>
    [Fact]
    public void TheLinkedPtxDefinesEveryWrapperItCalls()
    {
        var ptx = PtxFixtures.Linked();
        var called = LibDevicePostLink.WrappersCalled(ptx);

        Assert.NotEmpty(called);
        Assert.Equal(called.ToHashSet(StringComparer.Ordinal), LibDevicePostLink.WrappersDefined(ptx).ToHashSet(StringComparer.Ordinal));
    }

    /// <summary>Below compute 10.0, ILGPU's own PTX already defines every wrapper it calls: the post-link has nothing to compile.</summary>
    [Fact]
    public void BelowCompute10IlgpuDefinesEveryWrapperItCalls()
    {
        var ptx = PtxFixtures.BelowCompute10();
        var called = LibDevicePostLink.WrappersCalled(ptx);

        Assert.NotEmpty(called);
        Assert.Equal(called.ToHashSet(StringComparer.Ordinal), LibDevicePostLink.WrappersDefined(ptx).ToHashSet(StringComparer.Ordinal));
    }

    /// <summary>The same kernel calls the same wrappers in all three texts: the post-link adds definitions, not calls.</summary>
    [Fact]
    public void AllThreeTextsCallTheSameWrappers()
    {
        var native = LibDevicePostLink.WrappersCalled(PtxFixtures.Native()).ToHashSet(StringComparer.Ordinal);

        Assert.Equal(native, LibDevicePostLink.WrappersCalled(PtxFixtures.Linked()).ToHashSet(StringComparer.Ordinal));
        Assert.Equal(native, LibDevicePostLink.WrappersCalled(PtxFixtures.BelowCompute10()).ToHashSet(StringComparer.Ordinal));
    }

    /// <summary>
    /// No parameter name of a definition (<c>__ilgpu__nv_pow_param_0</c>, a line of its own ending in a comma) is read as a
    /// call, in the two texts that have definitions.
    /// </summary>
    [Fact]
    public void NoParameterNameIsReadAsACall()
    {
        foreach (var text in new[] { PtxFixtures.Linked(), PtxFixtures.BelowCompute10() })
        {
            Assert.DoesNotContain(LibDevicePostLink.WrappersCalled(text), name => name.Contains("param", StringComparison.OrdinalIgnoreCase));
        }
    }

    /// <summary>LF and CRLF line ends give the same called and defined sets, in all three texts.</summary>
    [Fact]
    public void TheInventoryIsTheSameWithLfAndCrlf()
    {
        foreach (var text in new[] { PtxFixtures.Native(), PtxFixtures.Linked(), PtxFixtures.BelowCompute10() })
        {
            var lf = text.ReplaceLineEndings("\n");
            var crlf = lf.ReplaceLineEndings("\r\n");
            Assert.Equal(LibDevicePostLink.WrappersCalled(lf), LibDevicePostLink.WrappersCalled(crlf));
            Assert.Equal(LibDevicePostLink.WrappersDefined(lf), LibDevicePostLink.WrappersDefined(crlf));
        }
    }
}

/// <summary>The three committed PTX texts of check L2, read from the test output directory.</summary>
internal static class PtxFixtures
{
    /// <summary>ILGPU 1.5.3's own PTX of <c>MathProbe.Probe</c> for the RTX 5070 Ti (<c>SM_120</c>).</summary>
    /// <returns>The text.</returns>
    public static string Native() => Read("probe.sm_120.ptx");

    /// <summary>The same PTX after <c>LibDevicePostLink.Link</c>.</summary>
    /// <returns>The text.</returns>
    public static string Linked() => Read("probe.sm_120.linked.ptx");

    /// <summary>ILGPU 1.5.3's own PTX of <c>MathProbe.Probe</c> for <c>SM_89</c>.</summary>
    /// <returns>The text.</returns>
    public static string BelowCompute10() => Read("probe.sm_89.ptx");

    private static string Read(string name) => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Devices", "Ptx", name));
}
