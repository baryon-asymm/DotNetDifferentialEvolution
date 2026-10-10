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

    /// <summary>
    /// Disposes the accelerator and the context when the lease owns them; does nothing for a borrowed one. The context is
    /// disposed even when the accelerator's disposal throws, and that exception then propagates (ACCEPTANCE.md, A6).
    /// </summary>
    public void Dispose()
    {
        try
        {
            if (_ownsAccelerator)
            {
                Accelerator.Dispose();
            }
        }
        finally
        {
            _ownedContext?.Dispose();
        }
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

/// <summary>
/// Releases that never stop at the first failure: every release runs in its own <c>try</c>, the failures are collected, and
/// only then does the owner throw them together or attach them to the exception it is already throwing (ACCEPTANCE.md,
/// Kernels node, check A6). Shared by the optimizer, the launchers and the bookkeeping, which all release device objects.
/// </summary>
internal static class ReleaseFailures
{
    /// <summary>The key under which a failed <c>Build</c> carries the failures of the releases that followed it.</summary>
    public const string DataKey = "DotNetDifferentialEvolution.GPU.ReleaseFailures";

    /// <summary>Runs <paramref name="release"/>; a failure is added to <paramref name="failures"/> and does not propagate.</summary>
    /// <param name="release">The release.</param>
    /// <param name="failures">Receives the failure, if any.</param>
    public static void Attempt(Action release, List<Exception> failures)
    {
        ArgumentNullException.ThrowIfNull(release);
        ArgumentNullException.ThrowIfNull(failures);
        try
        {
            release();
        }
        catch (Exception failure) when (Collect(failure, failures))
        {
            // Collected by the filter; the owner throws the failures together once everything has been released.
        }
    }

    /// <summary>Disposes every item in turn, whatever one of them throws.</summary>
    /// <param name="items">The items, in the order they are released.</param>
    /// <param name="failures">Receives the failures.</param>
    public static void Run(IEnumerable<IDisposable> items, List<Exception> failures)
    {
        ArgumentNullException.ThrowIfNull(items);
        foreach (var item in items)
        {
            Attempt(item.Dispose, failures);
        }
    }

    /// <summary>Throws the failures together, if there are any.</summary>
    /// <param name="failures">The collected failures.</param>
    /// <exception cref="AggregateException">There is at least one failure.</exception>
    public static void ThrowIfAny(IReadOnlyCollection<Exception> failures)
    {
        ArgumentNullException.ThrowIfNull(failures);
        if (failures.Count > 0)
        {
            throw new AggregateException(failures).Flatten();
        }
    }

    /// <summary>
    /// Records on <paramref name="original"/>, an exception being thrown because something failed to build, the failures of the
    /// releases that followed it, under <see cref="DataKey"/>. What it already holds from an inner owner is kept.
    /// </summary>
    /// <param name="original">The exception that is propagating; nothing else about it changes.</param>
    /// <param name="failures">The failures of the releases; nothing is recorded when empty.</param>
    public static void Attach(Exception original, IReadOnlyCollection<Exception> failures)
    {
        ArgumentNullException.ThrowIfNull(original);
        ArgumentNullException.ThrowIfNull(failures);
        if (failures.Count == 0)
        {
            return;
        }

        var all = original.Data[DataKey] is AggregateException earlier ? earlier.InnerExceptions.Concat(failures) : failures;
        original.Data[DataKey] = new AggregateException(all).Flatten();
    }

    /// <summary>
    /// The filter of a <c>catch (Exception)</c> that collects: it records <paramref name="failure"/> and says the catch is to
    /// take it. Every release must run whatever the one before threw, so the catch has to take every exception.
    /// </summary>
    /// <param name="failure">The exception being thrown.</param>
    /// <param name="failures">Receives it.</param>
    /// <returns>Always <see langword="true"/>.</returns>
    public static bool Collect(Exception failure, List<Exception> failures)
    {
        ArgumentNullException.ThrowIfNull(failure);
        ArgumentNullException.ThrowIfNull(failures);
        failures.Add(failure);
        return true;
    }
}
