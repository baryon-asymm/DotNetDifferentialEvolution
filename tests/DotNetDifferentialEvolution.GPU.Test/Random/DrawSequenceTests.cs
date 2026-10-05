namespace DotNetDifferentialEvolution.GPU.Test.Random;

/// <summary>
/// ACCEPTANCE.md, check 4b, on ILGPU's CPU accelerator: the first 10⁴ draws of a fixed (seed,
/// individual, generation) written by a kernel are the host's <c>PhiloxDraws</c> words bit for bit,
/// and a different seed, individual or generation gives a different sequence.
/// </summary>
[Trait("Category", "Integration")]
public class DrawSequenceTests
{
    private const int Seed = 20261003;
    private const int Individual = 17;
    private const int Generation = 5;

    /// <summary>The kernel on the CPU accelerator draws the host's words.</summary>
    [Fact]
    public void TheCpuAcceleratorDrawsTheHostWords()
    {
        using var context = TestDevices.CreateContext(GpuDevice.Cpu);
        using var accelerator = TestDevices.CreateAccelerator(context, GpuDevice.Cpu);

        var device = DrawSequences.OnDevice(accelerator, Seed, Individual, Generation);

        Assert.Equal(DrawSequences.OnHost(Seed, Individual, Generation), device);
    }

    /// <summary>
    /// Each coordinate of the stream matters: changing the seed, the individual or the generation
    /// changes the sequence, so the equality above is not of a constant stream.
    /// </summary>
    [Fact]
    public void ADifferentSeedIndividualOrGenerationGivesADifferentSequence()
    {
        var reference = DrawSequences.OnHost(Seed, Individual, Generation);

        Assert.NotEqual(reference, DrawSequences.OnHost(Seed + 1, Individual, Generation));
        Assert.NotEqual(reference, DrawSequences.OnHost(Seed, Individual + 1, Generation));
        Assert.NotEqual(reference, DrawSequences.OnHost(Seed, Individual, Generation + 1));
        Assert.True(reference.Distinct().Count() > DrawSequences.Length - 10, "the words repeat");
    }
}
