using DotNetDifferentialEvolution.GPU.Devices;
using ILGPU;
using ILGPU.Runtime;
using Xunit.Abstractions;

namespace DotNetDifferentialEvolution.GPU.Test.Devices;

/// <summary>
/// Check D2 of the GPU package's ACCEPTANCE.md: the package's math probe kernel
/// (<see cref="MathProbe.Probe"/>) on CUDA agrees with <see cref="Math"/> within 4 ULP for
/// <c>Exp</c>, <c>Log</c>, <c>Pow(x, 1.37)</c> and <c>Sqrt</c> on 10⁴ positive arguments. The
/// device is opened through the package's own <see cref="DeviceSelector"/> and the kernel loaded
/// through its <see cref="KernelLoader"/>, so on CUDA it compiles against libdevice and is completed
/// by the post-link, as a run's kernels are. The arguments are a
/// log-spaced grid over [1e-3, 700], where all four functions are finite. The same probe on
/// OpenCL is measured and reported, not held to a tolerance: D2 names CUDA only.
/// </summary>
/// <param name="output">Receives the largest ULP distance of each function and its argument.</param>
public class MathProbeTests(ITestOutputHelper output)
{
    /// <summary>D2's tolerance: APT's measurement for libdevice, not one chosen here.</summary>
    private const ulong ToleranceUlps = 4;

    private const int ArgumentCount = 10_000;
    private const double SmallestArgument = 1e-3;
    private const double LargestArgument = 700.0;

    private static readonly string[] FunctionNames = ["Exp", "Log", "Pow(x, 1.37)", "Sqrt"];

    /// <summary>D2: on CUDA, each of the four functions is within 4 ULP of <see cref="Math"/> on every argument.</summary>
    [Fact]
    [Trait("Category", "Gpu")]
    public void OnCudaTheFourFunctionsAreWithinFourUlpOfSystemMath()
    {
        var worst = Measure(Backend.Cuda);

        for (var f = 0; f < MathProbe.FunctionCount; f++)
        {
            Assert.True(
                worst[f].Ulps <= ToleranceUlps,
                $"{FunctionNames[f]} on CUDA is {worst[f].Ulps} ULP from System.Math at x = {worst[f].Argument:R}; the tolerance is {ToleranceUlps}.");
        }
    }

    /// <summary>
    /// Informative: the same probe on OpenCL. It asserts only that every result is a number, and
    /// reports the distances; no tolerance is frozen for OpenCL.
    /// </summary>
    [Fact]
    [Trait("Category", "Gpu")]
    public void OnOpenClTheProbeRunsAndItsDistancesAreReported()
    {
        var worst = Measure(Backend.OpenCL);

        for (var f = 0; f < MathProbe.FunctionCount; f++)
        {
            Assert.NotEqual(ulong.MaxValue, worst[f].Ulps);
        }
    }

    private static double[] Arguments()
    {
        var arguments = new double[ArgumentCount];
        var logSmallest = Math.Log(SmallestArgument);
        var logSpan = Math.Log(LargestArgument) - logSmallest;
        for (var k = 0; k < ArgumentCount; k++)
        {
            arguments[k] = Math.Exp(logSmallest + logSpan * (k + 0.5) / ArgumentCount);
        }

        return arguments;
    }

    private static double Reference(int function, double x) => function switch
    {
        0 => Math.Exp(x),
        1 => Math.Log(x),
        2 => Math.Pow(x, MathProbe.PowExponent),
        3 => Math.Sqrt(x),
        _ => throw new ArgumentOutOfRangeException(nameof(function), function, "The probe has four functions."),
    };

    private (ulong Ulps, double Argument)[] Measure(Backend backend)
    {
        var arguments = Arguments();
        double[] results;
        string deviceName;
        using (var lease = DeviceSelector.Open(backend))
        {
            var accelerator = lease.Accelerator;
            deviceName = accelerator.Name;
            using var kernel = KernelLoader.Load(accelerator, typeof(MathProbe).GetMethod(nameof(MathProbe.Probe))!);
            var probe = kernel.CreateLauncherDelegate<Action<AcceleratorStream, Index1D, ArrayView<double>, ArrayView<double>>>();
            using var inputs = accelerator.Allocate1D(arguments);
            using var outputs = accelerator.Allocate1D<double>(arguments.Length * MathProbe.FunctionCount);
            probe(accelerator.DefaultStream, arguments.Length, inputs.View, outputs.View);
            accelerator.Synchronize();
            results = outputs.GetAsArray1D();
        }

        var worst = new (ulong Ulps, double Argument)[MathProbe.FunctionCount];
        var exact = new int[MathProbe.FunctionCount];
        var within = new int[MathProbe.FunctionCount];
        var over = new int[MathProbe.FunctionCount];
        for (var k = 0; k < arguments.Length; k++)
        {
            for (var f = 0; f < MathProbe.FunctionCount; f++)
            {
                var distance = Ulp.Distance(results[MathProbe.FunctionCount * k + f], Reference(f, arguments[k]));
                var counter = distance == 0 ? exact : distance <= ToleranceUlps ? within : over;
                counter[f]++;
                if (distance > worst[f].Ulps)
                {
                    worst[f] = (distance, arguments[k]);
                }
            }
        }

        output.WriteLine($"{backend} ({deviceName}), {arguments.Length} arguments in [{SmallestArgument}, {LargestArgument}]:");
        for (var f = 0; f < MathProbe.FunctionCount; f++)
        {
            output.WriteLine(
                $"  {FunctionNames[f]}: max {worst[f].Ulps} ULP at x = {worst[f].Argument:R}; exact {exact[f]}, 1-{ToleranceUlps} ULP {within[f]}, over {over[f]}");
        }

        return worst;
    }
}
