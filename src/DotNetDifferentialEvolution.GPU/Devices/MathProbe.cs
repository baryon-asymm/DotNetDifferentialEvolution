using ILGPU;

namespace DotNetDifferentialEvolution.GPU.Devices;

/// <summary>
/// The math probe after APT's (<c>AerospacePropellantThermodynamics</c>, <c>src/Execution/MathProbe.cs</c>):
/// the transcendental functions of the allow-list evaluated inside a kernel, so a test can hold
/// a device's results against <see cref="Math"/> (ACCEPTANCE.md, check D2).
/// </summary>
internal static class MathProbe
{
    /// <summary>The exponent the probe passes to <see cref="Math.Pow"/>.</summary>
    public const double PowExponent = 1.37;

    /// <summary>Outputs per input, in the order Exp, Log, Pow(·, <see cref="PowExponent"/>), Sqrt.</summary>
    public const int FunctionCount = 4;

    /// <summary>Thread i writes the four functions of input i to outputs <c>[4i, 4i + 4)</c>.</summary>
    /// <param name="index">The input.</param>
    /// <param name="inputs">The arguments, positive.</param>
    /// <param name="outputs">The results, four per input.</param>
    public static void Probe(Index1D index, ArrayView<double> inputs, ArrayView<double> outputs)
    {
        int i = index;
        var x = inputs[i];
        outputs[FunctionCount * i] = Math.Exp(x);
        outputs[FunctionCount * i + 1] = Math.Log(x);
        outputs[FunctionCount * i + 2] = Math.Pow(x, PowExponent);
        outputs[FunctionCount * i + 3] = Math.Sqrt(x);
    }

    /// <summary>
    /// The trigonometric functions of the allow-list, as the samplers of JADE and SHADE call them (ACCEPTANCE.md, check
    /// S15, D3): thread i writes <c>Cos(cosines[i])</c> and <c>Tan(tangents[i])</c> to outputs <c>2i</c> and <c>2i + 1</c>.
    /// </summary>
    /// <param name="index">The input.</param>
    /// <param name="cosines">The arguments of <see cref="Math.Cos"/>.</param>
    /// <param name="tangents">The arguments of <see cref="Math.Tan"/>.</param>
    /// <param name="outputs">The results, two per input.</param>
    public static void TrigProbe(Index1D index, ArrayView<double> cosines, ArrayView<double> tangents, ArrayView<double> outputs)
    {
        int i = index;
        outputs[2 * i] = Math.Cos(cosines[i]);
        outputs[2 * i + 1] = Math.Tan(tangents[i]);
    }
}
