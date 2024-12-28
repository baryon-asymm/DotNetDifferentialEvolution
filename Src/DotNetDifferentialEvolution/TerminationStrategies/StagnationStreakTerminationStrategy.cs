using DotNetDifferentialEvolution.Models;
using DotNetDifferentialEvolution.TerminationStrategies.Interfaces;

namespace DotNetDifferentialEvolution.TerminationStrategies;

public class StagnationStreakTerminationStrategy : ITerminationStrategy
{
    public int MaxStagnationStreak { get; init; }
    
    public double Tolerance { get; init; }
    
    public int CurrentStagnationStreak { get; private set; }

    public double LastBestFitnessFunctionValue { get; private set; }
    
    public bool ShouldTerminate(Population population)
    {
        population.MoveCursorToBestIndividual();
        
        var difference = Math.Abs(population.IndividualCursor.FitnessFunctionValue - LastBestFitnessFunctionValue);
        if (difference > Tolerance)
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
