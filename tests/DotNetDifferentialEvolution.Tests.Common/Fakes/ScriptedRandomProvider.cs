namespace DotNetDifferentialEvolution.Tests.Common.Fakes;

/// <summary>
/// A <see cref="BaseRandomProvider"/> that replays pre-scripted values instead of drawing
/// random ones. This makes the otherwise stochastic building blocks (crossover, distinct
/// index selection, distribution sampling, control-parameter dithering) <b>exactly</b>
/// assertable in unit tests: the test author dictates every draw and predicts the result.
/// </summary>
/// <remarks>
/// <see cref="Next"/> and <see cref="NextDouble"/> consume independent queues. By default,
/// exhausting a queue throws so a test never silently depends on an unscripted draw; set
/// <see cref="CycleWhenExhausted"/> to repeat the scripted sequence instead.
/// </remarks>
/// <remarks>
/// Initializes a new instance.
/// </remarks>
/// <param name="ints">The values returned, in order, by <see cref="Next"/>.</param>
/// <param name="doubles">The values returned, in order, by <see cref="NextDouble"/>.</param>
public sealed class ScriptedRandomProvider(
    IEnumerable<int>? ints = null,
    IEnumerable<double>? doubles = null) : BaseRandomProvider
{
    private readonly IReadOnlyList<int> _ints = ints?.ToArray() ?? [];
    private readonly IReadOnlyList<double> _doubles = doubles?.ToArray() ?? [];

    private int _intCursor;
    private int _doubleCursor;

    /// <summary>
    /// When <see langword="true"/>, the scripted sequences wrap around instead of throwing
    /// once exhausted. Defaults to <see langword="false"/>.
    /// </summary>
    public bool CycleWhenExhausted { get; init; }

    /// <summary>Gets the number of <see cref="Next"/> calls served so far.</summary>
    public int IntDrawCount => _intCursor;

    /// <summary>Gets the number of <see cref="NextDouble"/> calls served so far.</summary>
    public int DoubleDrawCount => _doubleCursor;

    /// <inheritdoc />
    public override int Next(
        int maxValue)
    {
        var value = NextScripted(_ints, ref _intCursor, nameof(Next));
        return value < 0 || value >= maxValue
            ? throw new InvalidOperationException(
                $"Scripted Next value {value} is outside the requested range [0, {maxValue}).")
            : value;
    }

    /// <inheritdoc />
    public override double NextDouble() => NextScripted(_doubles, ref _doubleCursor, nameof(NextDouble));

    private T NextScripted<T>(
        IReadOnlyList<T> values,
        ref int cursor,
        string method)
    {
        if (values.Count == 0)
        {
            throw new InvalidOperationException($"No scripted values were provided for {method}.");
        }

        if (cursor >= values.Count)
        {
            if (!CycleWhenExhausted)
            {
                throw new InvalidOperationException(
                    $"Scripted values for {method} were exhausted after {values.Count} draw(s).");
            }

            cursor = 0;
        }

        return values[cursor++];
    }
}
