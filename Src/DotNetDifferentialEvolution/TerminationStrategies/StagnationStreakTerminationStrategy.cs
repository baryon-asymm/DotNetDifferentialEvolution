using DotNetDifferentialEvolution.Models;
using DotNetDifferentialEvolution.TerminationStrategies.Interfaces;

namespace DotNetDifferentialEvolution.TerminationStrategies;

/// <summary>
/// Represents a termination strategy that stops the evolution process when a stagnation streak is detected.
/// </summary>
public class StagnationStreakTerminationStrategy : ITerminationStrategy
{
    /// <summary>
    /// Gets the maximum number of generations allowed without improvement before termination.
    /// </summary>
    public int MaxStagnationStreak { get; init; }
    
    /// <summary>
    /// Gets the threshold for considering a change in fitness function value as significant.
    /// </summary>
    public double StagnationThreshold { get; init; }
    
    /// <summary>
    /// Gets the current number of generations without significant improvement.
    /// </summary>
    public int CurrentStagnationStreak { get; private set; }

    /// <summary>
    /// Gets the fitness function value of the best individual in the last generation.
    /// </summary>
    public double LastBestFitnessFunctionValue { get; private set; } = double.MinValue;
    
    /// <summary>
    /// Initializes a new instance of the <see cref="StagnationStreakTerminationStrategy"/> class.
    /// </summary>
    /// <param name="maxStagnationStreak">The maximum number of generations allowed without improvement before termination.</param>
    /// <param name="stagnationThreshold">The threshold for considering a change in fitness function value as significant.</param>
    public StagnationStreakTerminationStrategy(
        int maxStagnationStreak,
        double stagnationThreshold)
    {
        MaxStagnationStreak = maxStagnationStreak;
        StagnationThreshold = stagnationThreshold;
    }
    
    /// <summary>
    /// Determines whether the evolution process should terminate based on the current population.
    /// </summary>
    /// <param name="population">The current population of individuals.</param>
    /// <returns><c>true</c> if the evolution process should terminate; otherwise, <c>false</c>.</returns>
    public bool ShouldTerminate(Population population)
    {
        population.MoveCursorToBestIndividual();
        
        var difference = Math.Abs(population.IndividualCursor.FitnessFunctionValue - LastBestFitnessFunctionValue);
        if (difference > StagnationThreshold)
        {
            LastBestFitnessFunctionValue = population.IndividualCursor.FitnessFunctionValue;
            CurrentStagnationStreak = 0;
        }
        else
        {
            CurrentStagnationStreak++;
        }

        return CurrentStagnationStreak >= MaxStagnationStreak;
    }
}
