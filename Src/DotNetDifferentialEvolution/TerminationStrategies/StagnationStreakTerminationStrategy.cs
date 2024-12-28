using DotNetDifferentialEvolution.Models;
using DotNetDifferentialEvolution.TerminationStrategies.Interfaces;

namespace DotNetDifferentialEvolution.TerminationStrategies;

public class StagnationStreakTerminationStrategy : ITerminationStrategy
{
    public int MaxStagnationStreak { get; init; }
    
    public double StagnationThreshold { get; init; }
    
    public int CurrentStagnationStreak { get; private set; }

    public double LastBestFitnessFunctionValue { get; private set; } = double.MinValue;
    
    public StagnationStreakTerminationStrategy(
        int maxStagnationStreak,
        double stagnationThreshold)
    {
        MaxStagnationStreak = maxStagnationStreak;
        StagnationThreshold = stagnationThreshold;
    }
    
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
