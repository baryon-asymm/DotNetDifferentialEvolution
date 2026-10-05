namespace DotNetDifferentialEvolution.GPU.Devices;

/// <summary>The ILGPU backends the package runs on, in the order <see cref="DeviceSelector"/> tries them for Auto.</summary>
internal enum Backend
{
    /// <summary>An NVIDIA GPU through CUDA.</summary>
    Cuda = 0,

    /// <summary>A GPU through OpenCL.</summary>
    OpenCL = 1,

    /// <summary>ILGPU's CPU accelerator, which runs kernels on host threads.</summary>
    Cpu = 2,
}
