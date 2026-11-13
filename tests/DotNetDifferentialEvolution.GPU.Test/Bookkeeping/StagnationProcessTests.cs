using System.Diagnostics;
using DotNetDifferentialEvolution.GPU.Objectives;

namespace DotNetDifferentialEvolution.GPU.Test.Bookkeeping;

/// <summary>Exercises the public stagnation limit in a child process on each explicit backend.</summary>
public sealed class StagnationProcessTests
{
    /// <summary>The ILGPU CPU accelerator stops at the expected generation.</summary>
    [Fact]
    [Trait("Category", "Integration")]
    public Task CpuStopsOnConstantScores() => RunChild(GpuDevice.Cpu);

    /// <summary>The physical device stops at the expected generation.</summary>
    /// <param name="device">The backend selected through the public builder.</param>
    [Theory]
    [Trait("Category", "Gpu")]
    [InlineData(GpuDevice.Cuda)]
    [InlineData(GpuDevice.OpenCL)]
    public Task GpuStopsOnConstantScores(GpuDevice device) => RunChild(device);

    private static async Task RunChild(GpuDevice device)
    {
        var start = new ProcessStartInfo(Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") ?? "dotnet")
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardError = true,
        };
        start.ArgumentList.Add(typeof(StagnationProcessTests).Assembly.Location);
        start.ArgumentList.Add(device.ToString());
        using var child = Process.Start(start);
        Assert.NotNull(child);
        var error = child.StandardError.ReadToEndAsync();
        // Each objective builds a solver. Allow cold device compilation outside the generation guard.
        using var deadline = new CancellationTokenSource(TimeSpan.FromMinutes(2));
        try
        {
            await child.WaitForExitAsync(deadline.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            child.Kill(entireProcessTree: true);
            Assert.Fail($"{device} stagnation child exceeded two minutes. {await error.ConfigureAwait(false)}");
        }

        var details = await error.ConfigureAwait(false);
        Assert.True(child.ExitCode == 0, $"{device} stagnation child exited with {child.ExitCode}. {details}");
    }

    private static async Task<int> Main(string[] args)
    {
        if (args.Length == 0)
        {
            return 0;
        }

        var device = Enum.Parse<GpuDevice>(args[0]);
        double[] lower = [-1.0, -1.0];
        double[] upper = [1.0, 1.0];
        var failures = 0;
        foreach (var (score, threshold, expected) in new[]
        {
            (1.0, 0.001, 6),
            (double.PositiveInfinity, 0.001, 6),
            (double.NegativeInfinity, 0.001, 6),
            (double.NaN, 0.001, 5),
            (0.0, double.MaxValue, 5),             // First difference equals the threshold.
            (double.MaxValue, double.MaxValue, 6), // First difference overflows to +infinity.
        })
        {
            using var guard = new CancellationTokenSource();
            using var solver = GpuDifferentialEvolutionBuilder.ForFunction(new ConstantScore(score))
                .WithBounds(lower, upper)
                .WithPopulationSize(32)
                .WithShade(memorySize: 5)
                .WithStagnationLimit(5, threshold)
                .OnDevice(device)
                .WithSeed(7)
                .WithPopulationUpdateHandler(new GenerationGuard(guard))
                .Build();
            var scenario = $"{device}, score {score:R}, threshold {threshold:R}, expected generation {expected}";
            try
            {
                var result = await solver.RunAsync(guard.Token).ConfigureAwait(false);
                if (result.Generations != expected || !score.Equals(result.FitnessFunctionValue))
                {
                    await Console.Error.WriteLineAsync($"{scenario}: stopped at {result.Generations} with score {result.FitnessFunctionValue:R}.").ConfigureAwait(false);
                    failures++;
                }
            }
            catch (OperationCanceledException) when (guard.IsCancellationRequested)
            {
                await Console.Error.WriteLineAsync($"{scenario}: still running at generation 7.").ConfigureAwait(false);
                failures++;
            }
        }

        return failures;
    }

    internal readonly struct ConstantScore(double score) : IGpuFitnessFunction
    {
        public double Evaluate(GeneView genes) => score;
    }

    private sealed class GenerationGuard(CancellationTokenSource cancellation) : IGpuPopulationUpdatedHandler
    {
        public void Handle(GpuPopulationSnapshot snapshot)
        {
            ArgumentNullException.ThrowIfNull(snapshot);
            if (snapshot.Generation > 6)
            {
                cancellation.Cancel();
            }
        }
    }
}
