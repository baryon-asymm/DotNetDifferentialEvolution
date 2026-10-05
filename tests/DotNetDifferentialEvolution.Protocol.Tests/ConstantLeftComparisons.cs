using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace DotNetDifferentialEvolution.Protocol.Tests;

/// <summary>
/// ILGPU 1.5.3 moves a constant left operand of an ordered floating-point comparison (<c>&lt;</c>, <c>&lt;=</c>,
/// <c>&gt;</c>, <c>&gt;=</c>) to the right and inverts its NaN ordering while doing so, so <c>1.0 &lt; v</c> is true for
/// a NaN <c>v</c> on CUDA and false on the CPU. The rule (the GPU package's ACCEPTANCE.md, v1 check 8c): no literal and
/// no <c>const</c> stands on the left of an ordered comparison whose operands are both floating-point once converted. A
/// syntax walk alone cannot tell a floating-point operand from an integer one, so this builds a compilation over the
/// package's own files and reads the semantic model's converted type of each operand.
/// <para>Adapted from <c>AerospacePropellantThermodynamics</c>, commit <c>5fdd82c</c>,
/// <c>tests/Protocol.Tests/ConstantLeftComparisons.cs</c> (the third ILGPU defect of its root BOOT.md, 2026-09-28).
/// Changed here (this node's BOOT.md, Deviations from the kit):</para>
/// <list type="bullet">
/// <item>The references are the runtime's trusted platform assemblies (the test host's own dependency closure, which
/// holds ILGPU) instead of the assemblies already loaded in the process, which need not include ILGPU yet; the
/// package's own assembly is left out, since its sources are the ones compiled.</item>
/// <item>The SDK's implicit global usings are added, as the package's project enables them.</item>
/// <item>The language version is the package's pinned C# 12, not C# 14.</item>
/// <item>A compilation error is itself a problem: an operand typed as an error type would be read as not
/// floating-point and pass. The number of ordered floating-point comparisons read is returned, so a fact can refuse an
/// empty scan.</item>
/// </list>
/// </summary>
internal static class ConstantLeftComparisons
{
    private static readonly CSharpParseOptions ParseOptions = new(LanguageVersion.CSharp12);

    /// <summary>The global usings <c>Microsoft.NET.Sdk</c> generates for <c>ImplicitUsings</c>: the package's project
    /// enables them, and its files are compiled here without the SDK's generated file.</summary>
    private const string ImplicitUsings =
        "global using System;\nglobal using System.Collections.Generic;\nglobal using System.IO;\nglobal using System.Linq;\n" +
        "global using System.Net.Http;\nglobal using System.Threading;\nglobal using System.Threading.Tasks;\n";

    /// <summary>Every ordered floating-point comparison of the given files whose left operand is a literal or a
    /// <c>const</c>, one problem per occurrence naming the file and line, plus every compilation error; and the number of
    /// ordered floating-point comparisons read.</summary>
    public static (IReadOnlyList<string> Problems, int ComparisonsRead) Find(Node node, IReadOnlyList<string> files)
    {
        ArgumentNullException.ThrowIfNull(node);
        ArgumentNullException.ThrowIfNull(files);
        var compilation = Compile(node, files);
        var problems = compilation.GetDiagnostics()
            .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            .Select(diagnostic => $"{node.Name}: the compilation of the sources fails, so the semantic model cannot be trusted: {diagnostic}")
            .ToList();
        var read = 0;
        foreach (var tree in compilation.SyntaxTrees.Where(tree => tree.FilePath.Length > 0))
        {
            var model = compilation.GetSemanticModel(tree);
            foreach (var comparison in tree.GetRoot().DescendantNodes().OfType<BinaryExpressionSyntax>().Where(IsOrderedComparison))
            {
                if (!IsFloatingPoint(comparison.Left, model) || !IsFloatingPoint(comparison.Right, model))
                {
                    continue;
                }

                read++;
                if (IsConstant(comparison.Left, model))
                {
                    var line = tree.GetLineSpan(comparison.Span).StartLinePosition.Line + 1;
                    problems.Add($"{node.Name}: {Tree.Relative(tree.FilePath)}:{line}: '{comparison}' has a constant left of an ordered " +
                                 "floating-point comparison (ILGPU 1.5.3 swaps it to the right and inverts its NaN ordering)");
                }
            }
        }

        return (problems, read);
    }

    private static bool IsOrderedComparison(BinaryExpressionSyntax comparison) => comparison.Kind() is
        SyntaxKind.LessThanExpression or SyntaxKind.LessThanOrEqualExpression
        or SyntaxKind.GreaterThanExpression or SyntaxKind.GreaterThanOrEqualExpression;

    private static bool IsConstant(ExpressionSyntax expression, SemanticModel model) =>
        expression is LiteralExpressionSyntax { Token.Value: not null } || model.GetConstantValue(expression).HasValue;

    /// <summary>Whether the operand's type once the comparison's own implicit conversion is applied is <c>double</c> or
    /// <c>float</c>: <see cref="TypeInfo.ConvertedType"/>, not <see cref="TypeInfo.Type"/>, so an integer constant
    /// compared against a floating-point operand (<c>0 &lt; v</c>), which the compiler converts before the comparison
    /// runs, is caught like a literal already written as a <c>double</c>.</summary>
    private static bool IsFloatingPoint(ExpressionSyntax expression, SemanticModel model) =>
        model.GetTypeInfo(expression).ConvertedType?.SpecialType is SpecialType.System_Double or SpecialType.System_Single;

    private static CSharpCompilation Compile(Node node, IReadOnlyList<string> files)
    {
        var trees = files.Select(path => CSharpSyntaxTree.ParseText(File.ReadAllText(path), ParseOptions, path))
            .Append(CSharpSyntaxTree.ParseText(ImplicitUsings, ParseOptions));
        var options = new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable);
        return CSharpCompilation.Create(node.AssemblyName ?? "Sources", trees, References(node.AssemblyName), options);
    }

    /// <summary>The trusted platform assemblies of the running host, the compiled assembly's own name left out: the
    /// .NET runtime and the dependency closure of this test project, ILGPU among it.</summary>
    private static IEnumerable<MetadataReference> References(string? compiledAssemblyName)
    {
        var paths = AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") as string
                    ?? throw new InvalidOperationException("the host reports no trusted platform assemblies to compile against");
        return paths.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            .Where(path => !string.Equals(Path.GetFileNameWithoutExtension(path), compiledAssemblyName, StringComparison.OrdinalIgnoreCase))
            .Select(path => (MetadataReference)MetadataReference.CreateFromFile(path));
    }
}
