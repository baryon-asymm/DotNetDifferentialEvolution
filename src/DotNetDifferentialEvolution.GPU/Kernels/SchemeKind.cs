namespace DotNetDifferentialEvolution.GPU.Kernels;

/// <summary>The mutation scheme of a run, each the CPU package's class of the same name (docs/ALGORITHMS.md, §3.1, §5.1).</summary>
internal enum SchemeKind
{
    /// <summary>DE/rand/1: <c>x_r1 + F·(x_r2 − x_r3)</c>; the CPU package's <c>MutationStrategy</c> and <c>RandMutationStrategy</c>.</summary>
    RandOne = 0,

    /// <summary>DE/best/1: <c>x_best + F·(x_r1 − x_r2)</c>; <c>BestMutationStrategy</c>.</summary>
    Best = 1,

    /// <summary>DE/current-to-best/1: <c>x_i + F·(x_best − x_i) + F·(x_r1 − x_r2)</c>; <c>CurrentToBestMutationStrategy</c>.</summary>
    CurrentToBest = 2,

    /// <summary>DE/rand/2: <c>x_r1 + F·(x_r2 − x_r3) + F·(x_r4 − x_r5)</c>; <c>RandTwoMutationStrategy</c>.</summary>
    RandTwo = 3,

    /// <summary>DE/best/2: <c>x_best + F·(x_r1 − x_r2) + F·(x_r3 − x_r4)</c>; <c>BestTwoMutationStrategy</c>.</summary>
    BestTwo = 4,

    /// <summary>
    /// DE/current-to-pbest/1 with an archive: <c>x_i + F·(x_pbest − x_i) + F·(x_r1 − x̃_r2)</c>, with <c>x̃_r2</c> from
    /// the population and the archive; <c>CurrentToPBestMutationStrategy</c>.
    /// </summary>
    CurrentToPBest = 5,
}
