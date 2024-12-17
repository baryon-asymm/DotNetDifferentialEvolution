namespace DotNetDifferentialEvolution.Tests.Helpers;

public static class GenerateBoundsHelper
{
    public static ReadOnlyMemory<double> GenerateBounds(
        int length,
        double initialValue)
    {
        var bounds = new double[length];

        for (var i = 0; i < length; i++)
            bounds[i] = initialValue;

        return new ReadOnlyMemory<double>(bounds);
    }
}
