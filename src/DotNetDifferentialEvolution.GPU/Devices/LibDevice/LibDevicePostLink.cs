using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using ILGPU;
using ILGPU.Backends.PTX;
using ILGPU.Runtime.Cuda;

namespace DotNetDifferentialEvolution.GPU.Devices.LibDevice;

/// <summary>
/// Completes, rather than replaces, ILGPU's own libdevice wrappers in a compiled CUDA kernel: the one place in the
/// package that knows ILGPU's internals. ILGPU 1.5.3 emits calls to its <c>__ilgpu__nv_*</c> wrappers and, for compute
/// 10.0 and newer, defines none of them (BOOT.md). <see cref="Link"/> reads which wrappers a kernel calls and which it
/// already defines, compiles only the missing ones from ILGPU's own fragments through libnvvm, and trial-loads the result
/// so that a refusal carries the driver's log. Every libnvvm and driver result is checked through
/// <see cref="ThrowIfFailed(NvvmResult, string, string, string?)"/> or its <see cref="CudaError"/> overload. Adapted from
/// APThermo (<c>AerospacePropellantThermodynamics</c>, commit <c>5fdd82c</c>,
/// <c>src/Execution/LibDevice/LibDevicePostLink.cs</c>); here libnvvm is the one ILGPU's own CUDA backend loaded for the
/// context, so a caller-owned accelerator is completed with its own.
/// </summary>
internal static class LibDevicePostLink
{
    /// <summary>The ILGPU version whose internals this post-link was written against.</summary>
    public const string ExpectedIlgpuVersion = "1.5.3.0";

    private const string FragmentsType = "ILGPU.Backends.PTX.PTXLibDeviceNvvm";
    private const string FragmentsField = "fragments";
    private const string AssemblyField = "<PTXAssembly>k__BackingField";
    private const string WrapperPrefix = "__ilgpu";
    private const string TargetTriple = "target triple = \"nvptx64-unknown-cuda\"";
    private const string TargetDataLayout =
        "target datalayout = \"e-p:64:64:64-i1:8:8-i8:8:8-i16:16:16-i32:32:32-i64:64:64-f32:32:32-f64:64:64-v16:16:16-v32:32:32-v64:64:64-v128:128:128-n16:32:64\"";

    private static readonly Lazy<(FieldInfo Fragments, FieldInfo Assembly)> Members = new(() => AssertIlgpu(ExpectedIlgpuVersion));

    /// <summary>Gets the ILGPU version string of the loaded assembly.</summary>
    public static string IlgpuVersion => typeof(Context).Assembly.GetName().Version?.ToString() ?? "unknown";

    /// <summary>Asserts the ILGPU version and the reflected members once per process; an exception names the version.</summary>
    public static void AssertIlgpu() => _ = Members.Value;

    /// <summary>The assertion against a given version, for the tests to prove it fails loudly.</summary>
    /// <param name="expectedVersion">The version the internals are expected for.</param>
    /// <returns>The two reflected fields.</returns>
    /// <exception cref="InvalidOperationException">Another version is loaded, or a member is missing or of another type.</exception>
    internal static (FieldInfo Fragments, FieldInfo Assembly) AssertIlgpu(string expectedVersion)
    {
        var version = IlgpuVersion;
        if (version != expectedVersion)
        {
            throw new InvalidOperationException(
                $"ILGPU {version} is loaded, but the libdevice post-link was written for ILGPU {expectedVersion}; its internals must be re-verified.");
        }

        var fragments = typeof(PTXBackend).Assembly.GetType(FragmentsType)?.GetField(FragmentsField, BindingFlags.NonPublic | BindingFlags.Static);
        if (fragments is null || fragments.FieldType != typeof(Dictionary<string, string>))
        {
            throw new InvalidOperationException($"ILGPU {version}: {FragmentsType}.{FragmentsField} is not the dictionary of wrapper fragments the post-link expects.");
        }

        var backing = typeof(PTXCompiledKernel).GetField(AssemblyField, BindingFlags.NonPublic | BindingFlags.Instance);
        return backing is null || backing.FieldType != typeof(string)
            ? throw new InvalidOperationException($"ILGPU {version}: {nameof(PTXCompiledKernel)}.{AssemblyField} is not the string field the post-link expects.")
            : (fragments, backing);
    }

    /// <summary>
    /// The wrapper names a PTX text calls, from <c>call</c> instructions only, without the <c>__ilgpu</c> prefix (as the
    /// fragment keys are), distinct, in order of appearance.
    /// </summary>
    /// <param name="ptx">The PTX text.</param>
    /// <returns>The names called.</returns>
    public static IReadOnlyList<string> WrappersCalled(string ptx) =>
        [.. PtxText.CallSites(ptx).Select(name => name[WrapperPrefix.Length..]).Distinct()];

    /// <summary>The wrapper names a PTX text defines as its own <c>.func</c> headers, without the prefix, distinct.</summary>
    /// <param name="ptx">The PTX text.</param>
    /// <returns>The names defined.</returns>
    public static IReadOnlyList<string> WrappersDefined(string ptx) =>
        [.. PtxText.Definitions(ptx).Select(name => name[WrapperPrefix.Length..]).Distinct()];

    /// <summary>
    /// Completes the kernel's PTX with the wrappers it calls and ILGPU did not define, and trial-loads the result on either
    /// path (nothing missing, or something compiled). A kernel that calls no wrapper is returned untouched, without a trial
    /// load.
    /// </summary>
    /// <param name="accelerator">The accelerator whose backend compiled the kernel; its libnvvm compiles the wrappers.</param>
    /// <param name="compiled">The compiled kernel; its PTX is replaced when anything was inserted.</param>
    /// <returns>Which called wrappers ILGPU had defined, and which this method compiled.</returns>
    /// <exception cref="InvalidOperationException">A wrapper could not be completed, or the driver refused the result.</exception>
    public static LinkResult Link(CudaAccelerator accelerator, PTXCompiledKernel compiled)
    {
        ArgumentNullException.ThrowIfNull(accelerator);
        ArgumentNullException.ThrowIfNull(compiled);
        var ptx = compiled.PTXAssembly;
        var called = WrappersCalled(ptx);
        if (called.Count == 0)
        {
            return new LinkResult(compiled, [], []);
        }

        var missing = called.Except(WrappersDefined(ptx)).ToList();
        var arch = TargetArch(ptx);

        // The driver checks the PTX against the context bound to the calling thread.
        using var binding = accelerator.BindScoped();
        if (missing.Count == 0)
        {
            TrialLoad(ptx, arch);
            return new LinkResult(compiled, called, []);
        }

        var nvvm = accelerator.Backend.NvvmAPI
            ?? throw new InvalidOperationException(
                $"the libdevice post-link for {arch}: the kernel calls {string.Join(", ", missing)}, but the accelerator's context was built without LibDevice, so there is no libnvvm to compile them.");
        var body = WrapperBody(nvvm, missing, arch);
        AssertEveryWrapperDefined(body, missing);
        var linked = InsertAfterHeader(ptx, body);
        TrialLoad(linked, arch);
        Members.Value.Assembly.SetValue(compiled, linked);
        return new LinkResult(compiled, [.. called.Except(missing)], missing);
    }

    /// <summary>Throws the one failure shape for a libnvvm result; throws nothing for <see cref="NvvmResult.NVVM_SUCCESS"/>.</summary>
    /// <param name="result">The result.</param>
    /// <param name="call">The libnvvm call that returned it.</param>
    /// <param name="arch">The target.</param>
    /// <param name="log">The log, where one exists.</param>
    /// <exception cref="InvalidOperationException">The result is not a success.</exception>
    internal static void ThrowIfFailed(NvvmResult result, string call, string arch, string? log = null)
    {
        if (result != NvvmResult.NVVM_SUCCESS)
        {
            throw new InvalidOperationException(FailureMessage($"libnvvm {call} returned {result}", arch, log));
        }
    }

    /// <summary>The same shape for a CUDA driver result; throws nothing for <see cref="CudaError.CUDA_SUCCESS"/>.</summary>
    /// <param name="result">The result.</param>
    /// <param name="call">The driver call that returned it.</param>
    /// <param name="arch">The target.</param>
    /// <param name="log">The log, where one exists.</param>
    /// <exception cref="InvalidOperationException">The result is not a success.</exception>
    internal static void ThrowIfFailed(CudaError result, string call, string arch, string? log = null)
    {
        if (result != CudaError.CUDA_SUCCESS)
        {
            throw new InvalidOperationException(FailureMessage($"the CUDA driver's {call} returned {result}", arch, log));
        }
    }

    /// <summary>
    /// Checks that every wrapper in <paramref name="names"/> has a <c>.func</c> definition in <paramref name="body"/>, the
    /// text libnvvm produced, not the kernel's, which also calls the same names. The message names exactly the missing ones.
    /// </summary>
    /// <param name="body">The compiled wrapper text.</param>
    /// <param name="names">The wrappers it must define.</param>
    /// <exception cref="InvalidOperationException">A wrapper has no definition.</exception>
    internal static void AssertEveryWrapperDefined(string body, IReadOnlyList<string> names)
    {
        var defined = WrappersDefined(body).ToHashSet(StringComparer.Ordinal);
        var missing = names.Where(name => !defined.Contains(name)).ToList();
        if (missing.Count > 0)
        {
            throw new InvalidOperationException(
                $"the post-link produced no definition of the libdevice wrapper{(missing.Count > 1 ? "s" : string.Empty)} {string.Join(", ", missing)}.");
        }
    }

    /// <summary>
    /// The message: the post-link, the target, what failed, and a non-empty log where one exists, trimmed of white space
    /// and of the NUL padding of ILGPU's log buffer.
    /// </summary>
    private static string FailureMessage(string outcome, string arch, string? log)
    {
        var trimmed = log?.AsSpan().Trim("\0 \t\r\n").ToString();
        return string.IsNullOrEmpty(trimmed)
            ? $"the libdevice post-link for {arch}: {outcome}."
            : $"the libdevice post-link for {arch}: {outcome}: {trimmed}";
    }

    /// <summary>The kernel's own target, from its <c>.target sm_XX</c> line.</summary>
    private static string TargetArch(string ptx)
    {
        var sm = PtxText.TargetSm(ptx);
        return sm is not null
            ? "compute_" + sm
            : throw new InvalidOperationException("the kernel PTX has no .target line.");
    }

    /// <summary>The wrapper PTX libnvvm compiles from ILGPU's fragments, its module header stripped (the kernel has its own).</summary>
    private static string WrapperBody(NvvmAPI nvvm, IReadOnlyList<string> names, string arch)
    {
        var fragments = (Dictionary<string, string>)Members.Value.Fragments.GetValue(null)!;
        foreach (var name in names.Where(name => !fragments.ContainsKey(name)))
        {
            throw new InvalidOperationException($"the kernel calls the libdevice wrapper {name}, for which ILGPU {IlgpuVersion} has no fragment.");
        }

        ThrowIfFailed(nvvm.GetIRVersion(out var irMajor, out _, out _, out _), nameof(NvvmAPI.GetIRVersion), arch);
        var module = new StringBuilder();
        _ = module.Append(TargetTriple).Append('\n').Append(TargetDataLayout).Append('\n');
        _ = module.Append("!nvvmir.version = !{!0}\n!0 = !{i32 ").Append(irMajor).Append(", i32 0}\n");
        foreach (var name in names)
        {
            _ = module.Append(fragments[name]).Append('\n');
        }

        var wrapperPtx = CompileWrappers(nvvm, module.ToString(), arch);
        return string.Join('\n', wrapperPtx.Split('\n').Where(line => !IsModuleHeader(line)));
    }

    private static bool IsModuleHeader(string line) =>
        line.StartsWith(".version", StringComparison.Ordinal)
        || line.StartsWith(".target", StringComparison.Ordinal)
        || line.StartsWith(".address_size", StringComparison.Ordinal);

    private static string CompileWrappers(NvvmAPI nvvm, string module, string arch)
    {
        var moduleBytes = Encoding.ASCII.GetBytes(module);
        var libdevice = nvvm.LibDeviceBytes.ToArray();
        ThrowIfFailed(nvvm.CreateProgram(out var program), nameof(NvvmAPI.CreateProgram), arch);
        var succeeded = false;
        try
        {
            using var options = new NvvmOptions(arch);
            using var pinnedModule = new PinnedBytes(moduleBytes);
            using var pinnedLibdevice = new PinnedBytes(libdevice);
            ThrowIfFailed(
                nvvm.AddModuleToProgram(program, pinnedModule.Pointer, moduleBytes.Length, "dotnet-de-gpu-wrappers"),
                nameof(NvvmAPI.AddModuleToProgram),
                arch);
            ThrowIfFailed(
                nvvm.LazyAddModuleToProgram(program, pinnedLibdevice.Pointer, libdevice.Length, "libdevice"),
                nameof(NvvmAPI.LazyAddModuleToProgram),
                arch);
            var result = nvvm.CompileProgram(program, NvvmOptions.Count, options.Pointer);
            if (result != NvvmResult.NVVM_SUCCESS)
            {
                ThrowCompileFailure(nvvm, program, result, arch);
            }

            ThrowIfFailed(nvvm.GetCompiledResult(program, out var wrapperPtx), nameof(NvvmAPI.GetCompiledResult), arch);
            succeeded = true;
            return wrapperPtx ?? throw new InvalidOperationException($"libnvvm returned no PTX for the libdevice wrappers ({arch}).");
        }
        finally
        {
            // Checked only when the path before it succeeded: otherwise its result would replace the exception in flight.
            var released = nvvm.DestroyProgram(ref program);
            if (succeeded)
            {
                ThrowIfFailed(released, nameof(NvvmAPI.DestroyProgram), arch);
            }
        }
    }

    /// <summary>A failed compile throws with its log; if the log cannot be read, the failure still propagates, saying so.</summary>
    private static void ThrowCompileFailure(NvvmAPI nvvm, IntPtr program, NvvmResult result, string arch)
    {
        var logResult = nvvm.GetProgramLog(program, out var log);
        var reason = logResult == NvvmResult.NVVM_SUCCESS ? log : $"the log could not be read ({logResult})";
        ThrowIfFailed(result, nameof(NvvmAPI.CompileProgram), arch, reason);
    }

    /// <summary>The wrapper body spliced right after the kernel's module header: a callee must precede its call site.</summary>
    private static string InsertAfterHeader(string ptx, string body)
    {
        var headerEnd = ptx.IndexOf(".address_size", StringComparison.Ordinal);
        if (headerEnd < 0)
        {
            throw new InvalidOperationException("the kernel PTX has no .address_size line.");
        }

        headerEnd = ptx.IndexOf('\n', headerEnd) + 1;
        return string.Concat(ptx.AsSpan(0, headerEnd), body, "\n", ptx.AsSpan(headerEnd));
    }

    /// <summary>Loads the PTX once through the CUDA driver as a trial and destroys the module again.</summary>
    private static void TrialLoad(string ptx, string arch)
    {
        var loadResult = CudaAPI.CurrentAPI.LoadModule(out var handle, ptx, out var log);
        ThrowIfFailed(loadResult, nameof(CudaAPI.LoadModule), arch, log);
        ThrowIfFailed(CudaAPI.CurrentAPI.DestroyModule(handle), nameof(CudaAPI.DestroyModule), arch);
    }

    /// <summary>What <see cref="Link"/> did: which called wrappers ILGPU had defined, and which it compiled. Both empty when
    /// the kernel calls none.</summary>
    /// <param name="Kernel">The kernel, its PTX completed when anything was compiled.</param>
    /// <param name="DefinedByIlgpu">The called wrappers ILGPU had defined.</param>
    /// <param name="Compiled">The called wrappers this post-link compiled.</param>
    internal readonly record struct LinkResult(PTXCompiledKernel Kernel, IReadOnlyList<string> DefinedByIlgpu, IReadOnlyList<string> Compiled);

    /// <summary>A byte array pinned for the duration of a libnvvm call.</summary>
    private readonly struct PinnedBytes(byte[] bytes) : IDisposable
    {
        private readonly GCHandle _handle = GCHandle.Alloc(bytes, GCHandleType.Pinned);

        public IntPtr Pointer => _handle.AddrOfPinnedObject();

        public void Dispose() => _handle.Free();
    }

    /// <summary>The one-element libnvvm options array (<c>-arch=…</c>), owning its two unmanaged allocations.</summary>
    private readonly struct NvvmOptions : IDisposable
    {
        private readonly IntPtr _option;

        public NvvmOptions(string arch)
        {
            _option = Marshal.StringToHGlobalAnsi("-arch=" + arch);
            Pointer = Marshal.AllocHGlobal(IntPtr.Size);
            Marshal.WriteIntPtr(Pointer, _option);
        }

        public static int Count => 1;

        public IntPtr Pointer { get; }

        public void Dispose()
        {
            Marshal.FreeHGlobal(Pointer);
            Marshal.FreeHGlobal(_option);
        }
    }
}
