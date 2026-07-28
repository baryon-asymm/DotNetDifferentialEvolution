using System.Reflection;
using DotNetDifferentialEvolution.Models;

namespace DotNetDifferentialEvolution.UnitTests;

/// <summary>
/// Pins the parts of the public surface where source compatibility and binary compatibility come
/// apart, which no ordinary test notices: a call written as <c>RunAsync()</c> compiles happily
/// against an optional-parameter overload, so the whole suite stays green while the compiled
/// method every 4.0.0 consumer is bound to disappears from the assembly.
/// </summary>
/// <remarks>
/// Package validation against the published package is the real gate — this test exists so the
/// breakage is caught in the inner loop rather than at pack time, and so anyone tempted to merge
/// the two overloads back together has to read why they are separate.
/// </remarks>
[Trait("Category", "Unit")]
public class PublicApiCompatibilityTests
{
    [Fact]
    public void RunAsyncKeepsAParameterlessOverloadInTheCompiledSurface()
    {
        var parameterless = typeof(DifferentialEvolution).GetMethod(
            nameof(DifferentialEvolution.RunAsync),
            BindingFlags.Public | BindingFlags.Instance,
            binder: null,
            types: Type.EmptyTypes,
            modifiers: null);

        Assert.NotNull(parameterless);
        Assert.Equal(typeof(Task<Population>), parameterless.ReturnType);
    }

    [Fact]
    public void RunAsyncAlsoTakesACancellationToken()
    {
        var withToken = typeof(DifferentialEvolution).GetMethod(
            nameof(DifferentialEvolution.RunAsync),
            BindingFlags.Public | BindingFlags.Instance,
            binder: null,
            types: [typeof(CancellationToken)],
            modifiers: null);

        Assert.NotNull(withToken);
        Assert.Equal(typeof(Task<Population>), withToken.ReturnType);
    }
}
