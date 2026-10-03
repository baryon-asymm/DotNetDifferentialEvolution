using System.Reflection;
using Xunit;

namespace DotNetDifferentialEvolution.Protocol.Tests;

/// <summary>
/// The guards the GPU package's ACCEPTANCE.md freezes for v1 and places here: 5a (one transfer helper), 8a–8d (kernel
/// guards). Check 7a (no <c>GC.Collect</c>) is a rule of <see cref="ProtocolConfig.ForbiddenCallRules"/>, held by
/// <see cref="ForbiddenCallTests"/>. Every fact refuses an empty walk.
/// <para>Adapted from <c>AerospacePropellantThermodynamics</c>, commit <c>5fdd82c</c>,
/// <c>tests/Protocol.Tests/InvariantTests.cs</c> (the facts <c>NumericalNodesCallOnlyTheAllowedMathAndDoubleMembers</c>,
/// <c>NoSrcMethodPassesHostMemoryToAnIlgpuTransferByReference</c> and
/// <c>NumericalNodeSourcesPutNoConstantLeftOfAnOrderedFloatingComparison</c>) and <c>KernelReachability.cs</c>; the
/// changes are listed in this node's BOOT.md, Deviations from the kit.</para>
/// </summary>
public sealed class GpuGuardTests
{
    /// <summary>
    /// Check 5a: in the GPU package, only the methods of <see cref="ProtocolConfig.GpuTransferHelper"/> call an ILGPU host
    /// transfer, so a per-generation round trip cannot hide in the generation loop. Fails on an empty walk: with no
    /// transfer found at all, the helper is not the package's transfer path either.
    /// </summary>
    [Fact]
    public void OnlyTheTransferHelperCallsAnIlgpuHostTransfer()
    {
        var calls = IlgpuTransfers.Calls(GpuPackage.Assembly).ToList();
        Assert.True(calls.Count > 0, $"found nothing: no call to an ILGPU host transfer (CopyToCPU*, CopyFromCPU*) in {ProtocolConfig.GpuPackagePath}, so the walk proves nothing");
        var problems = calls
            .Where(call => TypeShape.Outermost(call.Method.DeclaringType!).FullName != ProtocolConfig.GpuTransferHelper)
            .Select(call => $"{ForbiddenCalls.CallerName(call.Method)} calls {IlgpuTransfers.Describe(call.Callee)}")
            .Distinct(StringComparer.Ordinal)
            .ToList();
        Assert.True(problems.Count == 0,
            $"ACCEPTANCE.md 5a: only {ProtocolConfig.GpuTransferHelper} may call an ILGPU host transfer.\n" + string.Join("\n", problems));
    }

    /// <summary>
    /// Check 8a: the IL reachable from each kernel entry point (<see cref="KernelReachability"/>) holds no <c>throw</c>,
    /// no <c>newarr</c>, no <c>newobj</c> of a reference type and no <c>box</c>, and every call it makes resolves. Fails
    /// on an empty walk.
    /// </summary>
    [Fact]
    public void KernelReachableCodeNeitherThrowsNorAllocatesNorBoxes()
    {
        var reached = Reached();
        var problems = KernelReachability.UnresolvedCalls(reached).Concat(KernelReachability.ForbiddenInstructions(reached)).ToList();
        Assert.True(problems.Count == 0,
            "ACCEPTANCE.md 8a: kernel-reachable IL holds no throw, newarr, newobj of a reference type or box.\n" + string.Join("\n", problems));
    }

    /// <summary>
    /// Check 8b: kernel-reachable code calls no member of <c>System.Math</c> or <c>System.Double</c> outside
    /// <see cref="ProtocolConfig.KernelMathAllowList"/>. Fails on an empty scan: kernel code with no such call at all
    /// would prove nothing.
    /// </summary>
    [Fact]
    public void KernelReachableCodeCallsOnlyTheAllowedMathAndDoubleMembers()
    {
        var reached = Reached();
        var calls = KernelReachability.MathCalls(reached).ToList();
        Assert.True(calls.Count > 0, "found nothing: kernel-reachable code calls no member of System.Math or System.Double, so the scan proves nothing");
        var problems = KernelReachability.UnresolvedCalls(reached)
            .Concat(calls
                .Where(call => !ProtocolConfig.KernelMathAllowList.Contains(call.Callee.Name))
                .Select(call => $"{call.Where}: calls {call.Callee.DeclaringType!.Name}.{call.Callee.Name}, outside the allow-list"))
            .ToList();
        Assert.True(problems.Count == 0,
            "ACCEPTANCE.md 8b: kernel code calls only " + string.Join(", ", ProtocolConfig.KernelMathAllowList.Order(StringComparer.Ordinal)) +
            " of System.Math and System.Double.\n" + string.Join("\n", problems));
    }

    /// <summary>
    /// Check 8c: no literal and no <c>const</c> stands on the left of an ordered floating-point comparison anywhere in
    /// the GPU package's sources (<see cref="ConstantLeftComparisons"/>). Fails on an empty scan: no source file, or no
    /// ordered floating-point comparison read.
    /// </summary>
    [Fact]
    public void GpuSourcesPutNoConstantLeftOfAnOrderedFloatingComparison()
    {
        var files = GpuPackage.SourceFiles();
        Assert.True(files.Count > 0, $"found nothing: no C# source under {ProtocolConfig.GpuPackagePath}, so the scan proves nothing");
        var (problems, read) = ConstantLeftComparisons.Find(GpuPackage.Node, files);
        Assert.True(read > 0, $"found nothing: no ordered floating-point comparison in the {files.Count} sources of {ProtocolConfig.GpuPackagePath}, so the scan proves nothing");
        Assert.True(problems.Count == 0,
            "ACCEPTANCE.md 8c: no constant left of an ordered floating-point comparison.\n" + string.Join("\n", problems));
    }

    /// <summary>
    /// Check 8d: no method of any <c>src</c> assembly passes host memory to an ILGPU host transfer as a raw <c>ref T</c>:
    /// ILGPU 1.5.3 turns such a reference into a raw pointer without pinning it, and a compacting collection moves the
    /// array under the copy. The span and array overloads pin it. Fails on an empty walk.
    /// </summary>
    [Fact]
    public void NoSrcMethodPassesHostMemoryToAnIlgpuTransferByReference()
    {
        var assemblies = NodeAssemblies.Assemblies.Where(pair => pair.Key.IsSource).Select(pair => pair.Value).Distinct().ToList();
        Assert.True(assemblies.Count > 0, "found nothing: no src assembly, so the walk proves nothing");
        var calls = assemblies.SelectMany(IlgpuTransfers.Calls).ToList();
        Assert.True(calls.Count > 0, "found nothing: no call to an ILGPU host transfer (CopyToCPU*, CopyFromCPU*) in any src assembly, so the walk proves nothing");
        var problems = calls
            .Where(call => call.Callee.GetParameters().Any(IlgpuTransfers.IsRawReference))
            .Select(call => $"{ForbiddenCalls.CallerName(call.Method)} calls {IlgpuTransfers.Describe(call.Callee)}")
            .Distinct(StringComparer.Ordinal)
            .ToList();
        Assert.True(problems.Count == 0,
            "ACCEPTANCE.md 8d: host memory crosses into ILGPU only through an overload that pins it (Span or array), never as a ref T.\n" +
            string.Join("\n", problems));
    }

    /// <summary>The kernel walk, refused when it is empty: no entry point, or an entry point whose body the walk cannot read.</summary>
    private static IReadOnlyList<(MethodBase Entry, MethodBase Method)> Reached()
    {
        var entries = KernelReachability.EntryPoints();
        Assert.True(entries.Count > 0, $"found nothing: no static method with a first parameter ILGPU.Index1D in {ProtocolConfig.GpuPackagePath}, so the walk proves nothing");
        var unread = entries.Where(entry => !IlBody.Instructions(entry).Any()).Select(KernelReachability.Name).ToList();
        Assert.True(unread.Count == 0, "found nothing: the walk reads no instruction of the kernel entry points " + string.Join(", ", unread));
        var reached = KernelReachability.Reach();
        Assert.True(reached.Count > entries.Count, "found nothing: the walk reached no method beyond the kernel entry points, so it proves nothing");
        return reached;
    }
}
