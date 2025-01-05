using DotNetDifferentialEvolution.Tests.Shared.FitnessFunctionEvaluators;

namespace DotNetDifferentialEvolution.Tests.WorkerControllerTesters;

public class RosenbrockException : Exception {}

public class ExceptionRosenbrockEvaluator : RosenbrockEvaluator
{
    private int _evaluationsCount;
    
    public int ThrowExceptionAt { get; init; }
    
    public ExceptionRosenbrockEvaluator(
        int throwExceptionAt)
    {
        ThrowExceptionAt = throwExceptionAt;
    }
    
    public override double Evaluate(
        ReadOnlySpan<double> genes)
    {
        var result = base.Evaluate(genes);
        
        if (Interlocked.Increment(ref _evaluationsCount) >= ThrowExceptionAt)
        {
            throw new RosenbrockException();
        }

        return result;
    }
}
