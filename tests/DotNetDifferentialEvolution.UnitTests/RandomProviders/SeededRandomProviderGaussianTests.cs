using DotNetDifferentialEvolution.RandomProviders;
using DotNetDifferentialEvolution.Tests.Shared.Fakes;

namespace DotNetDifferentialEvolution.UnitTests.RandomProviders;

/// <summary>
/// The Gaussian draw that drives JADE and SHADE parameter adaptation. Box–Muller produces two
/// independent normals per pair of uniforms and the transform used to discard one of them; these
/// tests hold the cached version to the same distribution, and pin the cache to the provider
/// instance rather than to the calling thread.
/// </summary>
[Trait("Category", "Unit")]
public class SeededRandomProviderGaussianTests
{
    [Fact]
    public void TheCachedValueIsTheOtherHalfOfTheSameTransform()
    {
        var random = new SeededRandomProvider(seed: 31);
        var first = random.NextGaussian(mean: 0.0, standardDeviation: 1.0);
        var second = random.NextGaussian(mean: 0.0, standardDeviation: 1.0);

        // Recompute the pair from the same two uniforms, taken from an identically seeded stream.
        var reference = new SeededRandomProvider(seed: 31);
        var radius = Math.Sqrt(-2.0 * Math.Log(1.0 - reference.NextDouble()));
        var angle = 2.0 * Math.PI * (1.0 - reference.NextDouble());

        Assert.Equal(radius * Math.Cos(angle), first, 1e-12);
        Assert.Equal(radius * Math.Sin(angle), second, 1e-12);
    }

    [Fact]
    public void APairOfDrawsConsumesTwoUniforms_NotFour()
    {
        // The point of the change: the second draw costs no randomness at all. A third draw
        // starts a new pair.
        var random = new SeededRandomProvider(seed: 5);
        random.NextGaussian(0.0, 1.0);
        random.NextGaussian(0.0, 1.0);

        var afterTwoGaussians = new SeededRandomProvider(seed: 5);
        afterTwoGaussians.NextDouble();
        afterTwoGaussians.NextDouble();

        Assert.Equal(afterTwoGaussians.NextULong(), random.NextULong());
    }

    [Fact]
    public void MeanAndDeviationAreApplied()
    {
        const int Samples = 200_000;

        var random = new SeededRandomProvider(seed: 17);
        var sum = 0.0;
        var sumOfSquares = 0.0;

        for (int i = 0; i < Samples; i++)
        {
            var value = random.NextGaussian(mean: 3.0, standardDeviation: 2.0);
            sum += value;
            sumOfSquares += value * value;
        }

        var mean = sum / Samples;
        var variance = sumOfSquares / Samples - mean * mean;

        Assert.Equal(3.0, mean, 0.03);
        Assert.Equal(4.0, variance, 0.06);
    }

    [Fact]
    public void TheOutputStillMatchesTheNormalDistribution()
    {
        // A Kolmogorov-Smirnov test against the exact normal CDF. Reusing the sine half of the
        // transform is only legitimate because that half is itself standard normal and
        // independent of the cosine half; if the caching were wrong — say it returned a stale
        // value, or the same value twice — this is what would catch it.
        const int Samples = 200_000;

        var random = new SeededRandomProvider(seed: 2024);
        var values = new double[Samples];
        for (int i = 0; i < Samples; i++)
            values[i] = random.NextGaussian(mean: 0.0, standardDeviation: 1.0);

        Array.Sort(values);

        var deviation = 0.0;
        for (int i = 0; i < Samples; i++)
        {
            var theoretical = NormalCdf(values[i]);
            deviation = Math.Max(deviation, Math.Abs((i + 1.0) / Samples - theoretical));
            deviation = Math.Max(deviation, Math.Abs(theoretical - (double)i / Samples));
        }

        // The 0.999 quantile of the KS statistic is about 1.95/sqrt(n).
        var critical = 1.95 / Math.Sqrt(Samples);

        Assert.True(deviation < critical, $"KS statistic {deviation:E3} exceeded {critical:E3}");
    }

    [Fact]
    public void ConsecutiveDrawsAreNotCorrelated()
    {
        // The cheap way to get this wrong is to return the same normal twice, which would pass a
        // distribution test on its own but shows up immediately as correlation between pairs.
        const int Pairs = 100_000;

        var random = new SeededRandomProvider(seed: 8);
        var sumOfProducts = 0.0;

        for (int i = 0; i < Pairs; i++)
        {
            var first = random.NextGaussian(0.0, 1.0);
            var second = random.NextGaussian(0.0, 1.0);
            sumOfProducts += first * second;
        }

        // For independent standard normals the mean product is 0 with standard error 1/sqrt(n).
        var correlation = sumOfProducts / Pairs;

        Assert.True(Math.Abs(correlation) < 4.0 / Math.Sqrt(Pairs),
            $"paired draws correlated at {correlation:E3}");
    }

    [Fact]
    public void TheCacheTravelsWithTheInstance_NotTheThread()
    {
        // Reproducibility depends on this. Two providers seeded alike must give the same Gaussian
        // sequence no matter which thread draws from them, and one provider's spare must never
        // satisfy another's draw.
        var first = new SeededRandomProvider(seed: 77);
        var second = new SeededRandomProvider(seed: 77);

        var interleaved = new List<double>();
        var straight = new List<double>();

        for (int i = 0; i < 100; i++)
        {
            interleaved.Add(first.NextGaussian(0.0, 1.0));
            straight.Add(second.NextGaussian(0.0, 1.0));
        }

        Assert.Equal(straight, interleaved);
    }

    [Fact]
    public void AThirdPartyProviderStillGetsThePlainTransform()
    {
        // BaseRandomProvider has nowhere to keep a spare, so a provider the engine did not create
        // keeps the original two-uniforms-per-draw behavior — which is what the scripted
        // closed-form tests depend on.
        var scripted = new ScriptedRandomProvider(doubles: [0.75, 0.0]);

        var value = RandomDistributionHelper.NextGaussian(scripted, mean: 2.0, standardDeviation: 3.0);

        var expected = Math.Sqrt(-2.0 * Math.Log(0.25)) * Math.Cos(2.0 * Math.PI);
        Assert.Equal(2.0 + 3.0 * expected, value, 1e-9);
        Assert.Equal(2, scripted.DoubleDrawCount);
    }

    [Fact]
    public void TheHelperRoutesTheEnginesProviderThroughTheCache()
    {
        var direct = new SeededRandomProvider(seed: 404);
        var viaHelper = new SeededRandomProvider(seed: 404);

        for (int i = 0; i < 50; i++)
        {
            Assert.Equal(
                direct.NextGaussian(1.5, 0.25),
                RandomDistributionHelper.NextGaussian(viaHelper, 1.5, 0.25));
        }
    }

    private static double NormalCdf(
        double x) => 0.5 * (1.0 + Erf(x / Math.Sqrt(2.0)));

    /// <summary>Abramowitz &amp; Stegun 7.1.26 — accurate to about 1.5e-7, well inside the test's tolerance.</summary>
    private static double Erf(
        double x)
    {
        var sign = Math.Sign(x);
        x = Math.Abs(x);

        const double A1 = 0.254829592;
        const double A2 = -0.284496736;
        const double A3 = 1.421413741;
        const double A4 = -1.453152027;
        const double A5 = 1.061405429;
        const double P = 0.3275911;

        var t = 1.0 / (1.0 + P * x);
        var y = 1.0 - (((((A5 * t + A4) * t) + A3) * t + A2) * t + A1) * t * Math.Exp(-x * x);

        return sign * y;
    }
}
