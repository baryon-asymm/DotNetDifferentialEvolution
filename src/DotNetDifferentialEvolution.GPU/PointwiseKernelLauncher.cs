using System.Reflection;
using DotNetDifferentialEvolution.GPU.Devices;
using DotNetDifferentialEvolution.GPU.Kernels;
using DotNetDifferentialEvolution.GPU.Objectives;
using ILGPU;
using ILGPU.Runtime;
using ILGPU.Util;

namespace DotNetDifferentialEvolution.GPU;

/// <summary>
/// The pointwise kernels compiled for <typeparamref name="TFunction"/> on one accelerator, each loaded through
/// <see cref="KernelLoader"/> as the single-kernel launcher's are. A generation is three launches (build, points, select),
/// the initialisation three (sample, points, combine). It owns the device buffers the path needs besides the population:
/// the <c>N_init·P</c> point results, the two per-individual F and CR buffers, and a stop word that is never set for the
/// initialisation's point launch; they are freed with the launcher.
/// </summary>
/// <typeparam name="TFunction">The pointwise objective.</typeparam>
/// <typeparam name="TPoint">The result of one point.</typeparam>
internal sealed class PointwiseKernelLauncher<TFunction, TPoint> : KernelLauncher
    where TFunction : struct, IGpuPointwiseFitnessFunction<TPoint>
    where TPoint : unmanaged
{
    private readonly AcceleratorStream _stream;
    private readonly TFunction _function;
    private readonly int _pointCount;
    private readonly List<IDisposable> _owned = [];
    private readonly PointwiseViews<TPoint> _points;
    private readonly ArrayView<int> _noStop;
    private readonly Action<AcceleratorStream, Index1D, StepParameters, PopulationViews> _sample;
    private readonly Action<AcceleratorStream, Index1D, TFunction, StepParameters, ArrayView<double>, PointwiseViews<TPoint>, ArrayView<int>> _evaluatePoints;
    private readonly Action<AcceleratorStream, Index1D, TFunction, StepParameters, PopulationViews, PointwiseViews<TPoint>> _combineInitial;
    private readonly Action<AcceleratorStream, Index1D, StepParameters, PopulationViews, StrategyViews, ArrayView<double>, ArrayView<double>> _buildTrials;
    private readonly Action<AcceleratorStream, Index1D, TFunction, StepParameters, PopulationViews, StrategyViews, PointwiseViews<TPoint>> _select;

    /// <summary>Compiles and loads the five kernels and allocates the buffers; ILGPU's compile errors and the post-link's surface here.</summary>
    /// <param name="accelerator">The accelerator.</param>
    /// <param name="function">The objective, passed to every launch.</param>
    /// <param name="pointCount"><c>P</c>, the number of points of an individual.</param>
    /// <param name="populationSize"><c>N_init</c>, the largest population of the run.</param>
    /// <param name="rule">The parameter rule the build kernel is compiled for.</param>
    public PointwiseKernelLauncher(Accelerator accelerator, TFunction function, int pointCount, int populationSize, ParameterRule rule = ParameterRule.Fixed)
    {
        ArgumentNullException.ThrowIfNull(accelerator);
        _stream = accelerator.DefaultStream;
        _function = function;
        _pointCount = pointCount;
        try
        {
            var results = Allocate<TPoint>(accelerator, (long)populationSize * pointCount);
            var forces = Allocate<double>(accelerator, populationSize);
            var probabilities = Allocate<double>(accelerator, populationSize);
            var noStop = Allocate<int>(accelerator, 1);
            noStop.MemSetToZero();
            _points = new PointwiseViews<TPoint>(results.View, pointCount, forces.View, probabilities.View);
            _noStop = noStop.View;

            var pointExtent = populationSize * pointCount;
            _sample = Load<Action<AcceleratorStream, Index1D, StepParameters, PopulationViews>>(
                accelerator, populationSize, nameof(PointwiseKernels.Sample));
            _evaluatePoints = Load<Action<AcceleratorStream, Index1D, TFunction, StepParameters, ArrayView<double>, PointwiseViews<TPoint>, ArrayView<int>>>(
                accelerator, pointExtent, nameof(PointwiseKernels.EvaluatePoints), typeof(TFunction), typeof(TPoint));
            _combineInitial = Load<Action<AcceleratorStream, Index1D, TFunction, StepParameters, PopulationViews, PointwiseViews<TPoint>>>(
                accelerator, populationSize, nameof(PointwiseKernels.CombineInitial), typeof(TFunction), typeof(TPoint));
            _buildTrials = Load<Action<AcceleratorStream, Index1D, StepParameters, PopulationViews, StrategyViews, ArrayView<double>, ArrayView<double>>>(
                accelerator, populationSize, nameof(PointwiseKernels.BuildTrials), RuleType(rule));
            _select = Load<Action<AcceleratorStream, Index1D, TFunction, StepParameters, PopulationViews, StrategyViews, PointwiseViews<TPoint>>>(
                accelerator, populationSize, nameof(PointwiseKernels.Select), typeof(TFunction), typeof(TPoint));
        }
        catch (Exception original)
        {
            // A release that fails here must not replace the exception that made it necessary (ACCEPTANCE.md, A6).
            ReleaseFailures.Attach(original, ReleaseOwned());
            throw;
        }
    }

    /// <inheritdoc />
    public override void Initialize(StepParameters parameters, PopulationViews views)
    {
        var individuals = parameters.PopulationSize;
        _sample(_stream, individuals, parameters, views);
        _evaluatePoints(_stream, individuals * _pointCount, _function, parameters, views.Current, _points, _noStop);
        _combineInitial(_stream, individuals, _function, parameters, views, _points);
    }

    /// <inheritdoc />
    public override void Generation(StepParameters parameters, PopulationViews views, StrategyViews strategy)
    {
        var individuals = parameters.PopulationSize;
        _buildTrials(_stream, individuals, parameters, views, strategy, _points.TrialMutationForces, _points.TrialCrossoverProbabilities);
        _evaluatePoints(_stream, individuals * _pointCount, _function, parameters, views.Trial, _points, strategy.Stop);
        _select(_stream, individuals, _function, parameters, views, strategy, _points);
    }

    /// <inheritdoc />
    public override void Dispose() => ReleaseFailures.ThrowIfAny(ReleaseOwned());

    /// <inheritdoc />
    internal override IReadOnlyList<DisposeBase> Allocated => [.. _owned.OfType<DisposeBase>()];

    private List<Exception> ReleaseOwned()
    {
        var failures = new List<Exception>();
        ReleaseFailures.Run(_owned, failures);
        _owned.Clear();
        return failures;
    }

    private MemoryBuffer1D<T, Stride1D.Dense> Allocate<T>(Accelerator accelerator, long length)
        where T : unmanaged
    {
        var buffer = accelerator.Allocate1D<T>(length);
        _owned.Add(buffer);
        return buffer;
    }

    private TDelegate Load<TDelegate>(Accelerator accelerator, int extent, string name, params Type[] typeArguments)
        where TDelegate : Delegate
    {
        var method = typeof(PointwiseKernels).GetMethod(name, BindingFlags.Public | BindingFlags.Static)!;
        var kernel = KernelLoader.Load(accelerator, typeArguments.Length == 0 ? method : method.MakeGenericMethod(typeArguments), extent);
        _owned.Add(kernel);
        return kernel.CreateLauncherDelegate<TDelegate>();
    }
}
