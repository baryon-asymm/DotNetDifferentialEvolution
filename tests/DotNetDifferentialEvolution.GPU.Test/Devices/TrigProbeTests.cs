using DotNetDifferentialEvolution.GPU.Devices;
using ILGPU;
using ILGPU.Runtime;
using Xunit.Abstractions;

namespace DotNetDifferentialEvolution.GPU.Test.Devices;

/// <summary>
/// Check S15 (D3) of the GPU package's ACCEPTANCE.md: on CUDA, <see cref="MathProbe.TrigProbe"/> agrees with
/// <see cref="Math"/> within 4 ULP, D2's tolerance, for <c>Cos</c> on [0, 2π] and <c>Tan</c> on [−π/2, π/2), 10⁴
/// arguments each. The arguments are those the samplers form: <c>2π·u</c> for the Gaussian with u = (k + 1)/10⁴, and
/// <c>π(u − 0.5)</c> for the Cauchy with u = k/10⁴. Opened and loaded as D2's probe is. OpenCL is measured and
/// reported, not held to a tolerance.
/// </summary>
/// <param name="output">Receives the largest ULP distance of each function and its argument.</param>
public class TrigProbeTests(ITestOutputHelper output)
{
    /// <summary>D2's tolerance, which S15 adopts.</summary>
    private const ulong ToleranceUlps = 4;

    private const int ArgumentCount = 10_000;

    private static readonly string[] FunctionNames = ["Cos", "Tan"];

    /// <summary>S15: on CUDA, Cos and Tan are within 4 ULP of <see cref="Math"/> on every argument.</summary>
    [Fact]
    [Trait("Category", "Gpu")]
    public void OnCudaCosAndTanAreWithinFourUlpOfSystemMath()
    {
        var worst = Measure(Backend.Cuda);

        for (var f = 0; f < FunctionNames.Length; f++)
        {
            Assert.True(
                worst[f].Ulps <= ToleranceUlps,
                $"{FunctionNames[f]} on CUDA is {worst[f].Ulps} ULP from System.Math at x = {worst[f].Argument:R}; the tolerance is {ToleranceUlps}.");
        }
    }

    /// <summary>Informative: the same probe on OpenCL; it asserts only that every result is a number.</summary>
    [Fact]
    [Trait("Category", "Gpu")]
    public void OnOpenClTheTrigProbeRunsAndItsDistancesAreReported()
    {
        var worst = Measure(Backend.OpenCL);

        Assert.All(worst, pair => Assert.NotEqual(ulong.MaxValue, pair.Ulps));
    }

    private (ulong Ulps, double Argument)[] Measure(Backend backend)
    {
        var cosines = new double[ArgumentCount];
        var tangents = new double[ArgumentCount];
        for (var k = 0; k < ArgumentCount; k++)
        {
            cosines[k] = 2.0 * Math.PI * ((k + 1) / (double)ArgumentCount);
            tangents[k] = Math.PI * (k / (double)ArgumentCount - 0.5);
        }

        double[] results;
        string deviceName;
        using (var lease = DeviceSelector.Open(backend))
        {
            var accelerator = lease.Accelerator;
            deviceName = accelerator.Name;
            using var kernel = KernelLoader.Load(accelerator, typeof(MathProbe).GetMethod(nameof(MathProbe.TrigProbe))!, ArgumentCount);
            var probe = kernel.CreateLauncherDelegate<Action<AcceleratorStream, Index1D, ArrayView<double>, ArrayView<double>, ArrayView<double>>>();
            using var cosineBuffer = accelerator.Allocate1D(cosines);
            using var tangentBuffer = accelerator.Allocate1D(tangents);
            using var outputs = accelerator.Allocate1D<double>(2 * ArgumentCount);
            probe(accelerator.DefaultStream, ArgumentCount, cosineBuffer.View, tangentBuffer.View, outputs.View);
            accelerator.Synchronize();
            results = outputs.GetAsArray1D();
        }

        var worst = new (ulong Ulps, double Argument)[FunctionNames.Length];
        for (var k = 0; k < ArgumentCount; k++)
        {
            Record(ref worst[0], Ulp.Distance(results[2 * k], Math.Cos(cosines[k])), cosines[k]);
            Record(ref worst[1], Ulp.Distance(results[2 * k + 1], Math.Tan(tangents[k])), tangents[k]);
        }

        output.WriteLine($"{backend} ({deviceName}), {ArgumentCount} arguments each:");
        for (var f = 0; f < FunctionNames.Length; f++)
        {
            output.WriteLine($"  {FunctionNames[f]}: max {worst[f].Ulps} ULP at x = {worst[f].Argument:R}");
        }

        return worst;
    }

    private static void Record(ref (ulong Ulps, double Argument) worst, ulong distance, double argument)
    {
        if (distance > worst.Ulps)
        {
            worst = (distance, argument);
        }
    }
}
