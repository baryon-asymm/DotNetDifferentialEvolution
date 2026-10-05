using DotNetDifferentialEvolution.GPU.Devices.LibDevice;
using ILGPU.Runtime.Cuda;

namespace DotNetDifferentialEvolution.GPU.Test.Devices;

/// <summary>
/// Check L3 of the GPU package's Devices/LibDevice/ACCEPTANCE.md: the post-link's check after compilation and its result-to-exception
/// check, both driven without a GPU. After APThermo's <c>PostLinkTests</c> (commit <c>5fdd82c</c>).
/// </summary>
public class PostLinkGuardTests
{
    private const string Arch = "compute_120";

    /// <summary>Two definitions in the shape libnvvm returns: the name right before its parameter list's parenthesis.</summary>
    private const string TwoDefinitions =
        ".visible .func  (.param .b64 func_retval0) __ilgpu__nv_exp(\n" +
        "\t.param .b64 __ilgpu__nv_exp_param_0\n" +
        ")\n" +
        "{\n" +
        "\tret;\n" +
        "}\n" +
        "\n" +
        ".visible .func  (.param .b64 func_retval0) __ilgpu__nv_log(\n" +
        "\t.param .b64 __ilgpu__nv_log_param_0\n" +
        ")\n" +
        "{\n" +
        "\tret;\n" +
        "}\n";

    private const string LogDefinitionStart = ".visible .func  (.param .b64 func_retval0) __ilgpu__nv_log(";

    /// <summary>Every wrapper with a definition passes.</summary>
    [Fact]
    public void EveryWrapperWithADefinitionPasses() =>
        LibDevicePostLink.AssertEveryWrapperDefined(TwoDefinitions, ["__nv_exp", "__nv_log"]);

    /// <summary>With one definition removed, the message names that wrapper and not the other.</summary>
    [Fact]
    public void AMissingDefinitionIsNamedAndOnlyIt()
    {
        var oneDefinition = TwoDefinitions[..TwoDefinitions.IndexOf(LogDefinitionStart, StringComparison.Ordinal)];

        var failure = Assert.Throws<InvalidOperationException>(() => LibDevicePostLink.AssertEveryWrapperDefined(oneDefinition, ["__nv_exp", "__nv_log"]));

        Assert.Contains("__nv_log", failure.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("__nv_exp", failure.Message, StringComparison.Ordinal);
    }

    /// <summary>A call site, which spells the name followed by a comma, is not taken for a definition.</summary>
    [Fact]
    public void ACallSiteIsNotADefinition()
    {
        var failure = Assert.Throws<InvalidOperationException>(
            () => LibDevicePostLink.AssertEveryWrapperDefined("call.uni (r), __ilgpu__nv_exp, (a);", ["__nv_exp"]));

        Assert.Contains("__nv_exp", failure.Message, StringComparison.Ordinal);
    }

    /// <summary>The success result of either library throws nothing.</summary>
    [Fact]
    public void SuccessThrowsNothing()
    {
        LibDevicePostLink.ThrowIfFailed(NvvmResult.NVVM_SUCCESS, "CompileProgram", Arch);
        LibDevicePostLink.ThrowIfFailed(CudaError.CUDA_SUCCESS, "LoadModule", Arch);
    }

    /// <summary>Every non-success libnvvm result names the post-link, libnvvm, the call, the result and the target.</summary>
    /// <param name="result">The result.</param>
    [Theory]
    [MemberData(nameof(NonSuccessNvvmResults))]
    public void EveryNvvmFailureNamesTheLibraryTheCallTheResultAndTheTarget(NvvmResult result)
    {
        var failure = Assert.Throws<InvalidOperationException>(() => LibDevicePostLink.ThrowIfFailed(result, "CompileProgram", Arch));

        Assert.Contains("the libdevice post-link", failure.Message, StringComparison.Ordinal);
        Assert.Contains("libnvvm", failure.Message, StringComparison.Ordinal);
        Assert.Contains("CompileProgram", failure.Message, StringComparison.Ordinal);
        Assert.Contains(result.ToString(), failure.Message, StringComparison.Ordinal);
        Assert.Contains(Arch, failure.Message, StringComparison.Ordinal);
    }

    /// <summary>Every non-success driver result names the post-link, the CUDA driver, the call, the result and the target.</summary>
    /// <param name="error">The result.</param>
    [Theory]
    [MemberData(nameof(NonSuccessCudaErrors))]
    public void EveryDriverFailureNamesTheLibraryTheCallTheResultAndTheTarget(CudaError error)
    {
        var failure = Assert.Throws<InvalidOperationException>(() => LibDevicePostLink.ThrowIfFailed(error, "LoadModule", Arch));

        Assert.Contains("the libdevice post-link", failure.Message, StringComparison.Ordinal);
        Assert.Contains("the CUDA driver", failure.Message, StringComparison.Ordinal);
        Assert.Contains("LoadModule", failure.Message, StringComparison.Ordinal);
        Assert.Contains(error.ToString(), failure.Message, StringComparison.Ordinal);
        Assert.Contains(Arch, failure.Message, StringComparison.Ordinal);
    }

    /// <summary>A log is carried, trimmed of white space and of the NUL padding ILGPU's log buffer returns.</summary>
    [Fact]
    public void ALogIsCarriedWithoutItsPadding()
    {
        var failure = Assert.Throws<InvalidOperationException>(
            () => LibDevicePostLink.ThrowIfFailed(NvvmResult.NVVM_ERROR_COMPILATION, "CompileProgram", Arch, "  a compiler diagnostic\0\0\0\0  "));

        Assert.Contains("a compiler diagnostic", failure.Message, StringComparison.Ordinal);
        Assert.DoesNotContain('\0', failure.Message);
    }

    /// <summary>A log that trims to nothing gives the same message as no log: no trailing ": ".</summary>
    [Fact]
    public void ALogThatTrimsToNothingIsNoLog()
    {
        var padded = Assert.Throws<InvalidOperationException>(
            () => LibDevicePostLink.ThrowIfFailed(NvvmResult.NVVM_ERROR_COMPILATION, "CompileProgram", Arch, "\0\0 \0"));
        var none = Assert.Throws<InvalidOperationException>(
            () => LibDevicePostLink.ThrowIfFailed(NvvmResult.NVVM_ERROR_COMPILATION, "CompileProgram", Arch));

        Assert.Equal(none.Message, padded.Message);
        Assert.EndsWith(".", padded.Message, StringComparison.Ordinal);
    }

    /// <summary>Every <see cref="NvvmResult"/> but success, read from the enum.</summary>
    /// <returns>The results.</returns>
    public static TheoryData<NvvmResult> NonSuccessNvvmResults() =>
        [.. Enum.GetValues<NvvmResult>().Where(result => result != NvvmResult.NVVM_SUCCESS)];

    /// <summary>Every <see cref="CudaError"/> but success, read from the enum.</summary>
    /// <returns>The results.</returns>
    public static TheoryData<CudaError> NonSuccessCudaErrors() =>
        [.. Enum.GetValues<CudaError>().Where(error => error != CudaError.CUDA_SUCCESS)];
}
