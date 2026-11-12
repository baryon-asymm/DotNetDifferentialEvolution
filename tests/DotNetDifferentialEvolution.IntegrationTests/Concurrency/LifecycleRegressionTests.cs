using System.Diagnostics;
using System.Globalization;
using DotNetDifferentialEvolution.Controllers;
using DotNetDifferentialEvolution.Interfaces;
using DotNetDifferentialEvolution.Models;

namespace DotNetDifferentialEvolution.IntegrationTests.Concurrency;

/// <summary>Exercises shutdown and reentry in child processes because a regression can kill or hang the caller.</summary>
[Trait("Category", "Integration")]
public sealed class LifecycleRegressionTests
{
    private static readonly TimeSpan Deadline = TimeSpan.FromSeconds(10);

    /// <summary>Runs the public API scenario with a deadline and reports the child's exception if it failed.</summary>
    /// <param name="scenario">The shutdown or reentry case to exercise.</param>
    /// <param name="workers">Number of solver workers.</param>
    [Theory]
    [InlineData("reject", 4)]
    [InlineData("reject-canceled", 4)]
    [InlineData("dispose", 1)]
    [InlineData("dispose", 4)]
    [InlineData("dispose-in-observer", 4)]
    public async Task ActiveRunLifecycleIsSafe(string scenario, int workers)
    {
        var start = new ProcessStartInfo(Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") ?? "dotnet")
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardError = true,
        };
        start.ArgumentList.Add(typeof(LifecycleRegressionTests).Assembly.Location);
        start.ArgumentList.Add(scenario);
        start.ArgumentList.Add(workers.ToString(CultureInfo.InvariantCulture));
        using var child = Process.Start(start);
        Assert.NotNull(child);
        var error = child.StandardError.ReadToEndAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        try
        {
            await child.WaitForExitAsync(timeout.Token).ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
            child.Kill(entireProcessTree: true);
            var partialError = await error.ConfigureAwait(true);
            Assert.Fail($"{scenario} with {workers} workers did not exit within 30 s. {partialError}");
        }

        var details = await error.ConfigureAwait(true);
        Assert.True(child.ExitCode == 0, $"{scenario} with {workers} workers exited with {child.ExitCode}: {details}");
    }

    private static async Task Main(string[] args)
    {
        if (args.Length == 0)
        {
            return;
        }

        var workers = int.Parse(args[1], CultureInfo.InvariantCulture);
        var baseline = WorkerController.GlobalWorkerCounter;
        var observer = new FirstGenerationObserver();
        using var de = CancellationTests.Build(observer, workers, int.MaxValue);
        switch (args[0])
        {
            case "reject":
            case "reject-canceled":
                await CheckRejectedCall(de, observer, args[0] == "reject-canceled").ConfigureAwait(false);
                break;
            case "dispose":
            case "dispose-in-observer":
                if (args[0] == "dispose-in-observer")
                {
                    observer.Action = de.Dispose;
                }

                var run = de.RunAsync();
                await observer.Entered.Task.WaitAsync(Deadline).ConfigureAwait(false);
                if (observer.Action is null)
                {
                    de.Dispose();
                }

                _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => run.WaitAsync(Deadline)).ConfigureAwait(false);
                Assert.True(run.IsCanceled);
                break;
            default:
                throw new ArgumentException("Unrecognized lifecycle scenario.", nameof(args));
        }

        de.Dispose();
        Assert.Equal(baseline, WorkerController.GlobalWorkerCounter);
        _ = Assert.Throws<ObjectDisposedException>(() => { _ = de.RunAsync(); });
    }

    private static async Task CheckRejectedCall(DifferentialEvolution de, FirstGenerationObserver observer, bool preCanceled)
    {
        using var release = new ManualResetEventSlim();
        observer.Action = () => Assert.True(release.Wait(Deadline));
        using var original = new CancellationTokenSource();
        var run = de.RunAsync(original.Token);
        try
        {
            await observer.Entered.Task.WaitAsync(Deadline).ConfigureAwait(false);
            _ = Assert.Throws<InvalidOperationException>(() => { _ = de.RunAsync(new CancellationToken(preCanceled)); });
            Assert.False(run.IsCompleted);
        }
        finally
        {
            await original.CancelAsync().ConfigureAwait(false);
            release.Set();
        }

        var canceled = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => run.WaitAsync(Deadline)).ConfigureAwait(false);
        Assert.True(run.IsCanceled);
        Assert.Equal(original.Token, canceled.CancellationToken);
    }

    private sealed class FirstGenerationObserver : IPopulationUpdatedHandler
    {
        internal TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        internal Action? Action { get; set; }

        public void Handle(Population population)
        {
            if (population.GenerationNumber == 1)
            {
                Entered.SetResult();
                Action?.Invoke();
            }
        }
    }
}
