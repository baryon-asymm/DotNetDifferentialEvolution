using DotNetDifferentialEvolution.RandomProviders;

namespace DotNetDifferentialEvolution.GPU.Test.Kernels;

/// <summary>
/// A CPU-package random provider that draws from a <see cref="SeededRandomProvider"/> and records
/// every call, so the same draws can be replayed into the GPU step (ACCEPTANCE.md, check 1g).
/// </summary>
/// <remarks>
/// With no worker provider on the context, the CPU step calls <see cref="Next"/> for an index and
/// <c>RandomThreshold.Scale(NextDouble())</c> for each crossover draw. Every double returned here is
/// <c>m · 2⁻⁵³</c> for a 53-bit <c>m</c>, so its scaled value is exactly <c>m · 2¹¹</c>, which is what
/// is recorded and what the GPU step is given as its 64-bit draw.
/// </remarks>
/// <param name="seed">The seed of the underlying generator.</param>
internal sealed class RecordingRandomProvider(int seed) : BaseRandomProvider
{
    private const double TwoToTheMinus53 = 1.0 / 9007199254740992.0;

    private readonly SeededRandomProvider _random = new(seed);
    private readonly List<ScriptedDraw> _calls = [];

    /// <summary>Gets the recorded calls, in order: an index with its range, or a 64-bit draw.</summary>
    public IReadOnlyList<ScriptedDraw> Calls => _calls;

    /// <inheritdoc />
    public override int Next(int maxValue)
    {
        var index = _random.Next(maxValue);
        _calls.Add(ScriptedDraw.Index(index, maxValue));
        return index;
    }

    /// <inheritdoc />
    public override double NextDouble()
    {
        var mantissa = _random.NextULong() >> 11;
        _calls.Add(ScriptedDraw.Word(mantissa << 11));
        return mantissa * TwoToTheMinus53;
    }
}
