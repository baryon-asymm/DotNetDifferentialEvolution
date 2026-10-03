namespace DotNetDifferentialEvolution.GPU.Test.EndToEnd;

/// <summary>
/// Check 5b of the GPU package's ACCEPTANCE.md, no per-generation host round trip, on the CPU
/// accelerator. The package's one transfer helper counts its population downloads
/// (<c>GpuDifferentialEvolution.PopulationDownloadCount</c>, internal): a 100-generation run with no
/// observer makes exactly one, the final population; with an observer every 10 generations it
/// makes 11.
/// </summary>
public class TransferCountTests
{
    private const int Generations = 100;

    private static readonly double[] Lower = [-5.0, -5.0];
    private static readonly double[] Upper = [5.0, 5.0];

    /// <summary>With no observer, <c>Build</c> downloads nothing and the run downloads once, at the end.</summary>
    /// <returns>The case.</returns>
    [Fact]
    public async Task ARunWithoutAnObserverDownloadsThePopulationOnce()
    {
        using var optimizer = Stage().Build();
        Assert.Equal(0, optimizer.PopulationDownloadCount);

        var result = await optimizer.RunAsync().ConfigureAwait(true);

        Assert.Equal(Generations, result.Generations);
        Assert.Equal(1, optimizer.PopulationDownloadCount);
    }

    /// <summary>With an observer every 10 generations, the run downloads 11 times: 10 snapshots and the final population.</summary>
    /// <returns>The case.</returns>
    [Fact]
    public async Task ARunWithAnObserverEveryTenGenerationsDownloadsElevenTimes()
    {
        var observer = new RecordingObserver();
        using var optimizer = Stage().WithPopulationUpdateHandler(observer, 10).Build();

        var result = await optimizer.RunAsync().ConfigureAwait(true);

        Assert.Equal(Generations, result.Generations);
        Assert.Equal([10, 20, 30, 40, 50, 60, 70, 80, 90, 100], observer.Generations);
        Assert.Equal(11, optimizer.PopulationDownloadCount);
    }

    private static IGpuDifferentialEvolutionBuilder<Sphere> Stage() =>
        GpuDifferentialEvolutionBuilder.ForFunction(default(Sphere))
            .WithBounds(Lower, Upper)
            .WithPopulationSize(16)
            .WithDefaultMutationStrategy(0.5, 0.9)
            .WithGenerationLimit(Generations)
            .OnDevice(GpuDevice.Cpu)
            .WithSeed(1);
}
