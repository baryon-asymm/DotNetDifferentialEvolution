namespace DotNetDifferentialEvolution.GPU;

/// <summary>The device a run asks for.</summary>
public enum GpuDevice
{
    /// <summary>CUDA, then OpenCL, then ILGPU's CPU accelerator: the first that opens.</summary>
    Auto = 0,

    /// <summary>An NVIDIA GPU through CUDA, or an error from <c>Build</c>.</summary>
    Cuda = 1,

    /// <summary>A GPU through OpenCL, or an error from <c>Build</c>.</summary>
    OpenCL = 2,

    /// <summary>ILGPU's CPU accelerator, which runs the kernels on host threads.</summary>
    Cpu = 3,
}
