# API.md — UnitTests/TestSupport

Namespace: `DotNetDifferentialEvolution.UnitTests.TestSupport`. Internal to the test
project.

## Population factory ✅

```csharp
internal static class PopulationFactory
{
    public static Population Create(double[] genes, double[] fitnessValues,
        int? bestIndividualIndex = null, int generationNumber = 0, long evaluationCount = 0);
    public static Population SingleIndividual(double[] fitnessValueBuffer);
}
```

The population wraps the arrays it is given, so a test can change a fitness value
after construction and the cursor sees it. Without an explicit best index, the index
of the minimum is used.
