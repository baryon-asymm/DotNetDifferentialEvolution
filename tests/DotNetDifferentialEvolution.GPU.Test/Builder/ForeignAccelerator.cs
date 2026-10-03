using ILGPU;
using ILGPU.Backends;
using ILGPU.Runtime;

namespace DotNetDifferentialEvolution.GPU.Test.Builder;

/// <summary>
/// A device of a type ILGPU 1.5.3 does not define. ILGPU reads a device's type from its public
/// <see cref="DeviceTypeAttribute"/>; <c>AcceleratorType</c> has only CPU, Cuda and OpenCL, so an
/// accelerator of another type exists only as a hand-made one like this.
/// </summary>
[DeviceType(ForeignAccelerator.ForeignType)]
internal sealed class ForeignDevice : Device
{
    /// <summary>Initializes the device.</summary>
    public ForeignDevice()
    {
        Name = "foreign";
    }

    /// <inheritdoc />
    public override Accelerator CreateAccelerator(Context context) => new ForeignAccelerator(context, this);
}

/// <summary>
/// The accelerator of <see cref="ForeignDevice"/>. It is never used to run anything: the builder
/// must reject it by its type, before any member below is reached.
/// </summary>
/// <param name="context">The context it is registered with.</param>
/// <param name="device">The foreign device.</param>
internal sealed class ForeignAccelerator(Context context, Device device) : Accelerator(context, device)
{
    /// <summary>The accelerator type: none of ILGPU's.</summary>
    public const AcceleratorType ForeignType = (AcceleratorType)99;

    /// <inheritdoc />
    public override TExtension CreateExtension<TExtension, TExtensionProvider>(TExtensionProvider provider) =>
        throw new NotSupportedException();

    /// <inheritdoc />
    protected override Kernel LoadKernelInternal(CompiledKernel kernel) => throw new NotSupportedException();

    /// <inheritdoc />
    protected override Kernel LoadImplicitlyGroupedKernelInternal(CompiledKernel kernel, int customGroupSize, out KernelInfo? kernelInfo) =>
        throw new NotSupportedException();

    /// <inheritdoc />
    protected override Kernel LoadAutoGroupedKernelInternal(CompiledKernel kernel, out KernelInfo? kernelInfo) =>
        throw new NotSupportedException();

    /// <inheritdoc />
    protected override bool CanAccessPeerInternal(Accelerator otherAccelerator) => false;

    /// <inheritdoc />
    protected override void EnablePeerAccessInternal(Accelerator otherAccelerator) => throw new NotSupportedException();

    /// <inheritdoc />
    protected override void DisablePeerAccessInternal(Accelerator otherAccelerator) => throw new NotSupportedException();

    /// <inheritdoc />
    protected override AcceleratorStream CreateStreamInternal() => throw new NotSupportedException();

    /// <inheritdoc />
    protected override void SynchronizeInternal()
    {
    }

    /// <inheritdoc />
    protected override MemoryBuffer AllocateRawInternal(long length, int elementSize) => throw new NotSupportedException();

    /// <inheritdoc />
    protected override int EstimateMaxActiveGroupsPerMultiprocessorInternal(Kernel kernel, int groupSize, int dynamicSharedMemorySizeInBytes) =>
        throw new NotSupportedException();

    /// <inheritdoc />
    protected override int EstimateGroupSizeInternal(
        Kernel kernel,
        Func<int, int> computeSharedMemorySize,
        int maxGroupSize,
        out int minGridSize) =>
        throw new NotSupportedException();

    /// <inheritdoc />
    protected override int EstimateGroupSizeInternal(Kernel kernel, int dynamicSharedMemorySizeInBytes, int maxGroupSize, out int minGridSize) =>
        throw new NotSupportedException();

    /// <inheritdoc />
    protected override PageLockScope<T> CreatePageLockFromPinnedInternal<T>(IntPtr pinned, long numElements) =>
        throw new NotSupportedException();

    /// <inheritdoc />
    protected override void DisposeAccelerator_SyncRoot(bool disposing)
    {
    }

    /// <inheritdoc />
    protected override void OnBind()
    {
    }

    /// <inheritdoc />
    protected override void OnUnbind()
    {
    }
}
