using DotNetDifferentialEvolution.GPU.Kernels;
using DotNetDifferentialEvolution.GPU.Objectives;
using DotNetDifferentialEvolution.GPU.Random;
using ILGPU.Runtime;

namespace DotNetDifferentialEvolution.GPU.Test.Kernels;

/// <summary>
/// ACCEPTANCE.md, check 2b: after one generation on ILGPU's CPU accelerator (N = 64, D = 4, fixed
/// seed), every slot i of the next population is, bit for bit, either parent i or trial i, and the
/// one <see cref="DeStep.Survives"/> picks. Trial i is recomputed on the host by
/// <see cref="DeStep.BuildTrial"/> from <c>PhiloxDraws(seed, i, 1)</c> over the downloaded initial
/// population, and evaluated on the host by the same objective.
/// </summary>
[Trait("Category", "Integration")]
public class GenerationSlotTests
{
    private const int PopulationSize = 64;
    private const int GenomeSize = 4;
    private const int Seed = 77;
    private const double MutationForce = 0.8;
    private const double CrossoverProbability = 0.9;

    /// <summary>Slot i holds parent i or trial i, as selection decides; both outcomes occur.</summary>
    [Fact]
    public void EverySlotHoldsItsParentOrItsTrial()
    {
        var lower = Enumerable.Repeat(-5.0, GenomeSize).ToArray();
        var upper = Enumerable.Repeat(5.0, GenomeSize).ToArray();
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
        var launcher = new KernelLauncher<ShiftedSphere>(accelerator, default);
        var parameters = new StepParameters(Seed, 0, PopulationSize, GenomeSize, MutationForce, DeStep.CrossoverThreshold(CrossoverProbability));

        launcher.Initialize(parameters, views);
        accelerator.Synchronize();
        var parents = new double[genes];
        var parentFitness = new double[PopulationSize];
        current.View.CopyToCPU(parents);
        currentFitness.View.CopyToCPU(parentFitness);
        launcher.Generation(parameters with { Generation = 1 }, views);
        accelerator.Synchronize();
        var survivors = new double[genes];
        var survivorFitness = new double[PopulationSize];
        next.View.CopyToCPU(survivors);
        nextFitness.View.CopyToCPU(survivorFitness);

        var trialsKept = 0;
        for (var i = 0; i < PopulationSize; i++)
        {
            var draws = new PhiloxDraws(Seed, i, 1);
            var hostTrial = step.BuildTrial(ref draws, i, MutationForce, CrossoverProbability, parents, lower, upper);
            using var hostTrialBuffer = step.Upload(hostTrial);
            var trialFitness = default(ShiftedSphere).Evaluate(new GeneView(hostTrialBuffer.View));
            var parent = parents[(i * GenomeSize)..((i + 1) * GenomeSize)];
            var slot = survivors[(i * GenomeSize)..((i + 1) * GenomeSize)];
            var expectTrial = DeStep.Survives(trialFitness, parentFitness[i]);

            Assert.True(SameBits(slot, hostTrial) || SameBits(slot, parent), $"slot {i} is neither its parent nor its trial");
            Assert.True(SameBits(slot, expectTrial ? hostTrial : parent), $"slot {i}: selection should keep the {(expectTrial ? "trial" : "parent")}");
            Assert.Equal(expectTrial ? trialFitness : parentFitness[i], survivorFitness[i]);
            trialsKept += expectTrial ? 1 : 0;
        }

        Assert.InRange(trialsKept, 1, PopulationSize - 1);
    }

    private static bool SameBits(double[] left, double[] right) =>
        left.Length == right.Length
        && left.Zip(right).All(pair => BitConverter.DoubleToInt64Bits(pair.First) == BitConverter.DoubleToInt64Bits(pair.Second));

    /// <summary>A kernel objective: <c>Σ (x_j − 1)²</c>.</summary>
    internal readonly struct ShiftedSphere : IGpuFitnessFunction
    {
        /// <inheritdoc />
        public double Evaluate(GeneView genes)
        {
            var sum = 0.0;
            for (var j = 0; j < genes.Length; j++)
            {
                var offset = genes[j] - 1.0;
                sum += offset * offset;
            }

            return sum;
        }
    }
}
