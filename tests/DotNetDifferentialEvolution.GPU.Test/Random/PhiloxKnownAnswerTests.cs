namespace DotNetDifferentialEvolution.GPU.Test.Random;

/// <summary>
/// ACCEPTANCE.md, check 3a, on the host and on ILGPU's CPU accelerator: Philox4x32-10 gives
/// Random123's known answers (<see cref="PhiloxKatVectors"/>, which cites the source:
/// <c>tests/kat_vectors</c>, lines 27–29, of https://github.com/DEShawResearch/random123 at
/// 9545ff6413f258be2f04c1d319d99aaef7521150).
/// </summary>
[Trait("Category", "Integration")]
public class PhiloxKnownAnswerTests
{
    /// <summary>The zero, all-ones and π vectors, computed on the host.</summary>
    /// <param name="vector">The vector: 0 zero, 1 all-ones, 2 π digits.</param>
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void TheHostGivesTheKnownAnswer(int vector) =>
        Assert.Equal(PhiloxKatVectors.ExpectedOf(vector), PhiloxKatVectors.OnHost(vector));

    /// <summary>The three vectors, computed inside a kernel on the CPU accelerator.</summary>
    [Fact]
    public void AKernelOnTheCpuAcceleratorGivesTheKnownAnswers()
    {
        using var context = TestDevices.CreateContext(GpuDevice.Cpu);
        using var accelerator = TestDevices.CreateAccelerator(context, GpuDevice.Cpu);

        var blocks = PhiloxKatVectors.RunKernel(accelerator);

        Assert.Equal(PhiloxKatVectors.Expected, blocks);
    }
}
