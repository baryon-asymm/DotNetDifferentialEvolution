namespace DotNetDifferentialEvolution.Tests.Common.FitnessFunctionEvaluators;

/// <summary>The exception thrown by <see cref="ExceptionRosenbrockEvaluator"/>.</summary>
public class RosenbrockException : Exception
{
    /// <summary>Initializes a new instance with the default message.</summary>
    public RosenbrockException() { }

    /// <summary>Initializes a new instance with the given message.</summary>
    /// <param name="message">The message that describes the error.</param>
    public RosenbrockException(string message) : base(message) { }

    /// <summary>Initializes a new instance with the given message and inner exception.</summary>
    /// <param name="message">The message that describes the error.</param>
    /// <param name="innerException">The exception that caused this one.</param>
    public RosenbrockException(string message, Exception innerException) : base(message, innerException) { }
}

/// <summary>
/// A <see cref="RosenbrockEvaluator"/> that throws once a configured number of evaluations
/// has been reached. Used to verify that fitness-function failures propagate cleanly out of
/// the worker threads (single- and multi-worker) as an <see cref="AggregateException"/>.
/// The evaluation counter is incremented atomically so the trigger is well-defined under
/// concurrent evaluation.
/// </summary>
public class ExceptionRosenbrockEvaluator(
    int throwExceptionAt,
    int dimension = 2) : RosenbrockEvaluator(dimension)
{
    private int _evaluationsCount;

    /// <summary>Gets the evaluation count at which the evaluator starts throwing.</summary>
    public int ThrowExceptionAt { get; init; } = throwExceptionAt;

    /// <summary>
    /// Evaluates the Rosenbrock function at <paramref name="genes"/>, then throws if this is
    /// evaluation number <see cref="ThrowExceptionAt"/> or later.
    /// </summary>
    /// <param name="genes">The point to evaluate, one value per dimension.</param>
    /// <returns>The function value at <paramref name="genes"/>.</returns>
    /// <exception cref="RosenbrockException">The evaluation count has reached <see cref="ThrowExceptionAt"/>.</exception>
    public override double Evaluate(
        ReadOnlySpan<double> genes)
    {
        var result = base.Evaluate(genes);

        return Interlocked.Increment(ref _evaluationsCount) >= ThrowExceptionAt ? throw new RosenbrockException() : result;
    }
}
