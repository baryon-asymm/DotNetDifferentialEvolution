namespace DotNetDifferentialEvolution.Tests.Shared.FitnessFunctionEvaluators;

/// <summary>The exception thrown by <see cref="ExceptionRosenbrockEvaluator"/>.</summary>
public class RosenbrockException : Exception
{
    public RosenbrockException() { }

    public RosenbrockException(string message) : base(message) { }

    public RosenbrockException(string message, Exception innerException) : base(message, innerException) { }
}

/// <summary>
/// A <see cref="RosenbrockEvaluator"/> that throws once a configured number of evaluations
/// has been reached. Used to verify that fitness-function failures propagate cleanly out of
/// the worker threads (single- and multi-worker) as an <see cref="AggregateException"/>.
/// The evaluation counter is incremented atomically so the trigger is well-defined under
/// concurrent evaluation.
/// </summary>
public class ExceptionRosenbrockEvaluator : RosenbrockEvaluator
{
    private int _evaluationsCount;

    /// <summary>Gets the evaluation count at which the evaluator starts throwing.</summary>
    public int ThrowExceptionAt { get; init; }

    public ExceptionRosenbrockEvaluator(
        int throwExceptionAt,
        int dimension = 2)
        : base(dimension)
    {
        ThrowExceptionAt = throwExceptionAt;
    }

    public override double Evaluate(
        ReadOnlySpan<double> genes)
    {
        var result = base.Evaluate(genes);

        if (Interlocked.Increment(ref _evaluationsCount) >= ThrowExceptionAt)
            throw new RosenbrockException();

        return result;
    }
}
