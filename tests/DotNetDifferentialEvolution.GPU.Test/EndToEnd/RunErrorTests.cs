namespace DotNetDifferentialEvolution.GPU.Test.EndToEnd;

/// <summary>
/// B1's row "the observer throws" of the GPU package's ACCEPTANCE.md: the task faults with that
/// exception, the same instance. The row "<c>RunAsync</c> while a run is in progress" is
/// <see cref="AsynchronyTests.ACallDuringTheRunThrows"/>.
/// </summary>
public class RunErrorTests
{
    private static readonly double[] Lower = [-5.0, -5.0];
    private static readonly double[] Upper = [5.0, 5.0];

    /// <summary>An observer that throws at generation 2 faults the run's task with its own exception.</summary>
    /// <returns>The case.</returns>
    [Fact]
    public async Task AThrowingObserverFaultsTheTaskWithItsException()
    {
        var thrown = new InvalidOperationException("The observer failed at generation 2.");
        using var optimizer = GpuDifferentialEvolutionBuilder.ForFunction(default(Sphere))
            .WithBounds(Lower, Upper)
            .WithPopulationSize(16)
            .WithDefaultMutationStrategy(0.5, 0.9)
            .WithGenerationLimit(10)
            .OnDevice(GpuDevice.Cpu)
            .WithSeed(1)
            .WithPopulationUpdateHandler(new ThrowingObserver(thrown, throwAtGeneration: 2))
            .Build();

        var run = optimizer.RunAsync();
        var failure = await Assert.ThrowsAsync<InvalidOperationException>(() => run).ConfigureAwait(true);

        Assert.Same(thrown, failure);
        Assert.True(run.IsFaulted);
    }
}
