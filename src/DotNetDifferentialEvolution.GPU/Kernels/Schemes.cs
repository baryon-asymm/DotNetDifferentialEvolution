using DotNetDifferentialEvolution.GPU.Random;

namespace DotNetDifferentialEvolution.GPU.Kernels;

/// <summary>
/// The trial of one individual under each <see cref="SchemeKind"/>: the CPU package's mutation strategy of the same name,
/// with its donor draws in its order and its arithmetic in its order, then its binomial crossover and repair
/// (<see cref="DeStep.CrossAndRepair"/>). The same draws give the same trial bit for bit (ACCEPTANCE.md, S2, S3).
/// </summary>
internal static class Schemes
{
    /// <summary>
    /// The smallest population each scheme can draw its donors from, the CPU strategies' <c>MinimumPopulationSize</c>:
    /// the individual and the distinct others it needs, or 4 for current-to-pbest.
    /// </summary>
    /// <param name="scheme">The scheme.</param>
    /// <returns>The minimum N.</returns>
    public static int MinimumPopulationSize(SchemeKind scheme) => scheme switch
    {
        SchemeKind.RandOne or SchemeKind.CurrentToPBest => 4,
        SchemeKind.Best or SchemeKind.CurrentToBest => 3,
        SchemeKind.BestTwo => 5,
        SchemeKind.RandTwo => 6,
        _ => throw new ArgumentOutOfRangeException(nameof(scheme), scheme, "Not a defined scheme."),
    };

    /// <summary>
    /// Builds the trial of <paramref name="individual"/> into its slot of <c>views.Trial</c> from <c>views.Current</c>.
    /// </summary>
    /// <typeparam name="TDraws">The draw source.</typeparam>
    /// <param name="draws">The individual's draws, after F and CR were drawn.</param>
    /// <param name="individual">The index i.</param>
    /// <param name="parameters">The scheme, N, D and the p-best range.</param>
    /// <param name="mutationForce">The trial's F.</param>
    /// <param name="crossoverThreshold">The trial's CR scaled to 64 bits.</param>
    /// <param name="views">The population; only slot i of the trial is written.</param>
    /// <param name="strategy">The best index, the ranking, the archive and its size.</param>
    public static void BuildTrial<TDraws>(
        ref TDraws draws,
        int individual,
        StepParameters parameters,
        double mutationForce,
        ulong crossoverThreshold,
        PopulationViews views,
        StrategyViews strategy)
        where TDraws : struct, IDrawSource
    {
        var populationSize = parameters.PopulationSize;
        var genomeSize = parameters.GenomeSize;
        var population = views.Current;
        var trial = views.Trial;
        var offset = individual * genomeSize;
        switch (parameters.Scheme)
        {
            case SchemeKind.Best:
                {
                    var r1 = DeStep.DrawDistinct(ref draws, individual, populationSize, -1, -1, -1, -1);
                    var r2 = DeStep.DrawDistinct(ref draws, individual, populationSize, r1, -1, -1, -1);
                    var best = strategy.BestIndex[0] * genomeSize;
                    for (var j = 0; j < genomeSize; j++)
                    {
                        trial[offset + j] = population[best + j]
                                            + mutationForce * (population[r1 * genomeSize + j] - population[r2 * genomeSize + j]);
                    }

                    break;
                }

            case SchemeKind.CurrentToBest:
                {
                    var r1 = DeStep.DrawDistinct(ref draws, individual, populationSize, -1, -1, -1, -1);
                    var r2 = DeStep.DrawDistinct(ref draws, individual, populationSize, r1, -1, -1, -1);
                    var best = strategy.BestIndex[0] * genomeSize;
                    for (var j = 0; j < genomeSize; j++)
                    {
                        var current = population[offset + j];
                        var mutant = current + mutationForce * (population[best + j] - current);
                        mutant += mutationForce * (population[r1 * genomeSize + j] - population[r2 * genomeSize + j]);
                        trial[offset + j] = mutant;
                    }

                    break;
                }

            case SchemeKind.RandTwo:
                {
                    var r1 = DeStep.DrawDistinct(ref draws, individual, populationSize, -1, -1, -1, -1);
                    var r2 = DeStep.DrawDistinct(ref draws, individual, populationSize, r1, -1, -1, -1);
                    var r3 = DeStep.DrawDistinct(ref draws, individual, populationSize, r1, r2, -1, -1);
                    var r4 = DeStep.DrawDistinct(ref draws, individual, populationSize, r1, r2, r3, -1);
                    var r5 = DeStep.DrawDistinct(ref draws, individual, populationSize, r1, r2, r3, r4);
                    for (var j = 0; j < genomeSize; j++)
                    {
                        var mutant = population[r1 * genomeSize + j]
                                     + mutationForce * (population[r2 * genomeSize + j] - population[r3 * genomeSize + j]);
                        mutant += mutationForce * (population[r4 * genomeSize + j] - population[r5 * genomeSize + j]);
                        trial[offset + j] = mutant;
                    }

                    break;
                }

            case SchemeKind.BestTwo:
                {
                    var r1 = DeStep.DrawDistinct(ref draws, individual, populationSize, -1, -1, -1, -1);
                    var r2 = DeStep.DrawDistinct(ref draws, individual, populationSize, r1, -1, -1, -1);
                    var r3 = DeStep.DrawDistinct(ref draws, individual, populationSize, r1, r2, -1, -1);
                    var r4 = DeStep.DrawDistinct(ref draws, individual, populationSize, r1, r2, r3, -1);
                    var best = strategy.BestIndex[0] * genomeSize;
                    for (var j = 0; j < genomeSize; j++)
                    {
                        var mutant = population[best + j]
                                     + mutationForce * (population[r1 * genomeSize + j] - population[r2 * genomeSize + j]);
                        mutant += mutationForce * (population[r3 * genomeSize + j] - population[r4 * genomeSize + j]);
                        trial[offset + j] = mutant;
                    }

                    break;
                }

            case SchemeKind.CurrentToPBest:
                CurrentToPBest(ref draws, individual, parameters, mutationForce, views, strategy);
                break;

            case SchemeKind.RandOne:
            default:
                {
                    DeStep.PickDonors(ref draws, individual, populationSize, out var r1, out var r2, out var r3);
                    for (var j = 0; j < genomeSize; j++)
                    {
                        trial[offset + j] = population[r1 * genomeSize + j]
                                            + mutationForce * (population[r2 * genomeSize + j] - population[r3 * genomeSize + j]);
                    }

                    break;
                }
        }

        DeStep.CrossAndRepair(ref draws, individual, genomeSize, crossoverThreshold, population, trial, views.LowerBound, views.UpperBound);
    }

    /// <summary>
    /// <c>round(value)</c> half away from zero for a non-negative value, as
    /// <c>Math.Round(value, MidpointRounding.AwayFromZero)</c>: the fraction above the floor is exact, and at 0.5 or
    /// more the value rounds up.
    /// </summary>
    /// <param name="value">A non-negative value.</param>
    /// <returns>The rounded value.</returns>
    public static double RoundHalfAwayFromZero(double value)
    {
        var floor = Math.Floor(value);
        return value - floor >= 0.5 ? floor + 1.0 : floor;
    }

    /// <summary>
    /// The CPU package's <c>CurrentToPBestMutationStrategy.Mutate</c> up to the crossover: p drawn from [min, max] when they
    /// differ; <c>topCount = clamp(round(p·N), min(2, N), N)</c>; <c>pbest = ranking[u(topCount)]</c>; r1 from N, redrawn
    /// while it is i; r2 from N + archive size, redrawn while it is i or r1, from the archive when it is N or more.
    /// </summary>
    private static void CurrentToPBest<TDraws>(
        ref TDraws draws,
        int individual,
        StepParameters parameters,
        double mutationForce,
        PopulationViews views,
        StrategyViews strategy)
        where TDraws : struct, IDrawSource
    {
        var populationSize = parameters.PopulationSize;
        var genomeSize = parameters.GenomeSize;
        var population = views.Current;
        var pBestRate = parameters.PBestRateMin < parameters.PBestRateMax
            ? parameters.PBestRateMin + draws.NextUnitDouble() * (parameters.PBestRateMax - parameters.PBestRateMin)
            : parameters.PBestRateMin;
        var topCount = Math.Max(
            Math.Min(2, populationSize),
            Math.Min((int)RoundHalfAwayFromZero(pBestRate * populationSize), populationSize));
        var pBest = strategy.Ranking[draws.NextIndex(topCount)];

        int r1;
        do
        {
            r1 = draws.NextIndex(populationSize);
        }
        while (r1 == individual);

        var unionSize = populationSize + strategy.ArchiveSize[0];
        int r2;
        do
        {
            r2 = draws.NextIndex(unionSize);
        }
        while (r2 == individual || r2 == r1);

        var secondDifference = r2 < populationSize ? population : strategy.Archive;
        var secondOffset = (r2 < populationSize ? r2 : r2 - populationSize) * genomeSize;
        var offset = individual * genomeSize;
        for (var j = 0; j < genomeSize; j++)
        {
            var current = population[offset + j];
            var mutant = current + mutationForce * (population[pBest * genomeSize + j] - current);
            mutant += mutationForce * (population[r1 * genomeSize + j] - secondDifference[secondOffset + j]);
            views.Trial[offset + j] = mutant;
        }
    }
}
