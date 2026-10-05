using DotNetDifferentialEvolution.GPU.Bookkeeping;
using DotNetDifferentialEvolution.GPU.Kernels;
using DotNetDifferentialEvolution.GPU.Objectives;
using DotNetDifferentialEvolution.GPU.Random;
using ILGPU.Runtime;

namespace DotNetDifferentialEvolution.GPU.Test.Kernels;

/// <summary>
/// ACCEPTANCE.md, check S6: after one jDE generation on ILGPU's CPU accelerator (N = 64, D = 4, fixed seed), F_i and CR_i
/// are the values trial i used exactly where the trial replaced its parent, ties included, and unchanged elsewhere. The
/// expected values are recomputed on the host from <c>PhiloxDraws(seed, i, 1)</c>: <see cref="ControlParameters.Jde"/>,
/// then the trial, then the objective and <see cref="Selection.Outcome"/>. The objective is stepped so that ties occur;
/// improvements, ties and kept parents each occur, and a tie whose trial drew a new F or CR, asserted: jDE keeps the
/// parent's F and CR nine times in ten, so without such a tie an inheritance on improvement only would pass.
/// </summary>
[Trait("Category", "Integration")]
public class JdeInheritanceTests
{
    private const int PopulationSize = 64;
    private const int GenomeSize = 4;
    private const int Seed = 91;
    private const double InitialMutationForce = 0.5;
    private const double InitialCrossoverProbability = 0.9;

    /// <summary>F_i and CR_i follow the trial exactly where it replaced the parent.</summary>
    [Fact]
    public void TheTrialsParametersPassToTheIndividualExactlyWhereItReplacedTheParent()
    {
        var lower = Enumerable.Repeat(-2.0, GenomeSize).ToArray();
        var upper = Enumerable.Repeat(2.0, GenomeSize).ToArray();
        var genes = PopulationSize * GenomeSize;
        using var step = new HostStep();
        var accelerator = step.Accelerator;
        using var current = accelerator.Allocate1D<double>(genes);
        using var currentFitness = accelerator.Allocate1D<double>(PopulationSize);
        using var next = accelerator.Allocate1D<double>(genes);
        using var nextFitness = accelerator.Allocate1D<double>(PopulationSize);
        using var trial = accelerator.Allocate1D<double>(genes);
        using var lowerBuffer = step.Upload(lower);
        using var upperBuffer = step.Upload(upper);
        var views = new PopulationViews(current.View, currentFitness.View, next.View, nextFitness.View, trial.View, lowerBuffer.View, upperBuffer.View);
        using var launcher = new KernelLauncher<SteppedSphere>(accelerator, default, ParameterRule.Jde);
        using var bookkeeping = new GenerationBookkeeping(
            accelerator,
            new BookkeepingPlan(
                PopulationSize, GenomeSize, SchemeKind.RandOne, ParameterRule.Jde, 0, 0, 0.0, false, InitialMutationForce, InitialCrossoverProbability, null),
            Seed);
        var parameters = new StepParameters(Seed, 0, PopulationSize, GenomeSize, double.NaN, 0UL, SchemeKind.RandOne, ParameterRule.Jde);

        launcher.Initialize(parameters, views);
        accelerator.Synchronize();
        var parents = new double[genes];
        var parentFitness = new double[PopulationSize];
        current.View.CopyToCPU(parents);
        currentFitness.View.CopyToCPU(parentFitness);
        launcher.Generation(parameters with { Generation = 1 }, views, bookkeeping.Views);
        accelerator.Synchronize();
        var forces = new double[PopulationSize];
        var crossovers = new double[PopulationSize];
        bookkeeping.Views.MutationForces.CopyToCPU(forces);
        bookkeeping.Views.CrossoverProbabilities.CopyToCPU(crossovers);

        var outcomes = new int[3];
        var tiesWithNewParameters = 0;
        for (var i = 0; i < PopulationSize; i++)
        {
            var draws = new PhiloxDraws(Seed, i, 1);
            ControlParameters.Jde(ref draws, InitialMutationForce, InitialCrossoverProbability, out var usedF, out var usedCr);
            var hostTrial = step.BuildSchemeTrial(
                ref draws, i, parameters with { Generation = 1 }, usedF, usedCr, parents, lower, upper, SchemeState.WithBest(0));
            using var hostTrialBuffer = step.Upload(hostTrial);
            var trialFitness = default(SteppedSphere).Evaluate(new GeneView(hostTrialBuffer.View));
            var outcome = Selection.Outcome(trialFitness, parentFitness[i], acceptsTies: true);
            outcomes[outcome]++;
            if (outcome == Selection.Accepted && (usedF != InitialMutationForce || usedCr != InitialCrossoverProbability))
            {
                tiesWithNewParameters++;
            }

            var replaced = outcome != Selection.Kept;
            ParityCases.AssertSameBits(replaced ? usedF : InitialMutationForce, forces[i], $"F of individual {i} ({outcome})");
            ParityCases.AssertSameBits(replaced ? usedCr : InitialCrossoverProbability, crossovers[i], $"CR of individual {i} ({outcome})");
        }

        Assert.True(outcomes.All(count => count > 0), $"kept {outcomes[0]}, tied {outcomes[1]}, improved {outcomes[2]}");
        Assert.True(tiesWithNewParameters > 0, $"no tie drew a new F or CR (ties {outcomes[1]})");
    }

    /// <summary>⌊Σ x_j² / 4⌋: a sphere in steps of 4, so that a trial often ties its parent.</summary>
    internal readonly struct SteppedSphere : IGpuFitnessFunction
    {
        /// <inheritdoc />
        public double Evaluate(GeneView genes)
        {
            var sum = 0.0;
            for (var j = 0; j < genes.Length; j++)
            {
                sum += genes[j] * genes[j];
            }

            return Math.Floor(sum / 4.0);
        }
    }
}
