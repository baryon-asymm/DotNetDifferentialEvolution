using ILGPU;
using ILGPU.Runtime;

namespace DotNetDifferentialEvolution.GPU.Devices;

/// <summary>
/// An accelerator together with who owns it. An opened one owns its context and accelerator and
/// disposes both; a borrowed one is the caller's and is never disposed (BOOT.md, invariant 7).
/// </summary>
internal sealed class AcceleratorLease : IDisposable
{
    private readonly Context? _ownedContext;
    private readonly bool _ownsAccelerator;

    private AcceleratorLease(Context? ownedContext, Accelerator accelerator, bool ownsAccelerator, Backend backend, string? fallbackReason)
    {
        _ownedContext = ownedContext;
        _ownsAccelerator = ownsAccelerator;
        Accelerator = accelerator;
        Backend = backend;
        FallbackReason = fallbackReason;
    }

    /// <summary>Gets the accelerator.</summary>
    public Accelerator Accelerator { get; }

    /// <summary>Gets the backend the accelerator runs on.</summary>
    public Backend Backend { get; }

    /// <summary>Gets why Auto skipped the backends before this one, or <see langword="null"/> when it skipped none or the device was explicit.</summary>
    public string? FallbackReason { get; }

    /// <summary>A lease that owns <paramref name="context"/> and <paramref name="accelerator"/>.</summary>
    /// <param name="context">The context, disposed with the lease.</param>
    /// <param name="accelerator">The accelerator, disposed with the lease.</param>
    /// <param name="backend">The backend.</param>
    /// <param name="fallbackReason">Why Auto skipped earlier backends.</param>
    /// <returns>The lease.</returns>
    public static AcceleratorLease Owned(Context context, Accelerator accelerator, Backend backend, string? fallbackReason) =>
        new(context, accelerator, ownsAccelerator: true, backend, fallbackReason);

    /// <summary>A lease on the caller's accelerator, which it never disposes.</summary>
    /// <param name="accelerator">The caller's accelerator.</param>
    /// <returns>The lease.</returns>
    /// <exception cref="ArgumentException">The accelerator is not a CUDA, OpenCL or CPU one.</exception>
    public static AcceleratorLease Borrowed(Accelerator accelerator) =>
        new(null, accelerator, ownsAccelerator: false, BackendOf(accelerator), null);

    /// <summary>Disposes the accelerator and the context when the lease owns them; does nothing for a borrowed one.</summary>
    public void Dispose()
    {
        if (_ownsAccelerator)
        {
            Accelerator.Dispose();
        }

        _ownedContext?.Dispose();
    }

    private static Backend BackendOf(Accelerator accelerator) => accelerator.AcceleratorType switch
    {
        AcceleratorType.Cuda => Backend.Cuda,
        AcceleratorType.OpenCL => Backend.OpenCL,
        AcceleratorType.CPU => Backend.Cpu,
        _ => throw new ArgumentException(
            $"The accelerator is of type {accelerator.AcceleratorType}; only CUDA, OpenCL and CPU accelerators are supported.",
            nameof(accelerator)),
    };
}
