namespace DotNetDifferentialEvolution.GPU.Test.EndToEnd;

/// <summary>
/// The objectives of the convergence cases, checked on the host against their closed forms, so a
/// red 1h means the optimizer and not the objective.
/// </summary>
public class ObjectiveTests
{
    /// <summary>The series cosine agrees with <see cref="Math.Cos"/> to 1e-13 over four periods, and is exactly 1 at integers.</summary>
    [Fact]
    public void TheRastriginCosineMatchesSystemMath()
    {
        for (var k = -2000; k <= 2000; k++)
        {
            var x = k / 500.0;
            Assert.Equal(Math.Cos(2.0 * Math.PI * x), Rastrigin.CosTwoPi(x), 1e-13);
        }

        Assert.Equal(1.0, Rastrigin.CosTwoPi(0.0));
        Assert.Equal(1.0, Rastrigin.CosTwoPi(-3.0));
    }
}
