namespace DotNetDifferentialEvolution.GPU;

/// <summary>The device a run is on, and why <see cref="GpuDevice.Auto"/> skipped the devices before it.</summary>
/// <param name="Kind">The backend: <see cref="GpuDevice.Cuda"/>, <see cref="GpuDevice.OpenCL"/> or <see cref="GpuDevice.Cpu"/>, never <see cref="GpuDevice.Auto"/>.</param>
/// <param name="Name">The device's name as ILGPU reports it.</param>
/// <param name="FallbackReason">
/// Why Auto skipped each backend it tried before this one; <see langword="null"/> when it skipped
/// none, when the device was explicit, and for a caller's accelerator.
/// </param>
public sealed record GpuDeviceInfo(GpuDevice Kind, string Name, string? FallbackReason);
