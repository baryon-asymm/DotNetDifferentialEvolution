using BenchmarkDotNet.Running;
using DotNetDifferentialEvolution.Benchmark;
using DotNetDifferentialEvolution.Benchmark.BenchmarkTesters;

// "convergence" runs a solution-quality comparison across all variants;
// otherwise the default BenchmarkDotNet throughput micro-benchmark runs.
if (args.Length > 0 && args[0].Equals("convergence", StringComparison.OrdinalIgnoreCase))
    ConvergenceComparison.Run();
else
    BenchmarkRunner.Run<SimpleSumTester>();
