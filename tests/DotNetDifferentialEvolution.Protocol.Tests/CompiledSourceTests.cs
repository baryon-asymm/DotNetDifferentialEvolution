using Xunit;

namespace DotNetDifferentialEvolution.Protocol.Tests;

/// <summary>
/// Placement (AGENTS.md §1, the tree invariant): every C# source the compiler actually read lives in a directory that
/// holds both documents. The linter walks directories and skips the ones it treats as build output; this reads the
/// compiler's own record (<see cref="CompiledSources"/>), so a source hidden in a skipped directory cannot escape.
/// </summary>
public sealed class CompiledSourceTests
{
    /// <summary>Every compiled source lives in a node directory.</summary>
    [Fact]
    public void EveryCompiledSourceLivesInANodeDirectory()
    {
        var sources = CompiledSources.All;
        Assert.True(sources.Count > 0, "found nothing: no compiled C# source under the tree root was named by any PDB, so the walk proves nothing " +
                                       "(are the PDBs portable and the source paths unmapped?)");
        var problems = sources.Select(path => Path.GetDirectoryName(path)!).Distinct(StringComparer.OrdinalIgnoreCase)
            .Where(directory => !(File.Exists(Path.Combine(directory, "BOOT.md")) && File.Exists(Path.Combine(directory, "API.md"))))
            .Select(directory => $"{Tree.Relative(directory)}: holds a compiled C# source but no BOOT.md and API.md (AGENTS.md §1)")
            .ToList();
        Assert.True(problems.Count == 0, string.Join("\n", problems));
    }

    /// <summary>The one exclusion beyond <c>bin</c>/<c>obj</c> is narrow: only a source below a package folder is the
    /// package's; a plain source, one in a skipped-looking directory, and one under a sentinel at the root stay the tree's.</summary>
    [Fact]
    public void OnlyASourceBelowAPackageFolderIsThePackages()
    {
        var scratch = Directory.CreateTempSubdirectory("protocol-checks-compiled-sources-").FullName;
        try
        {
            var root = scratch + Path.DirectorySeparatorChar;
            var package = Path.Combine(scratch, ".nuget-packages", "some.package", "1.0.0");
            var packageSource = WriteSource(Path.Combine(package, "build", "net8.0", "Program.cs"));
            File.WriteAllText(Path.Combine(package, ".nupkg.metadata"), "{}");
            var treeSource = WriteSource(Path.Combine(scratch, "src", "Node", "Code.cs"));
            var templatesSource = WriteSource(Path.Combine(scratch, "src", "Node", "templates", "Code.cs"));

            Assert.True(CompiledSources.IsPackageOwned(packageSource, root), "a source below a package folder was read as the tree's");
            Assert.False(CompiledSources.IsPackageOwned(treeSource, root), "a plain node source was read as a package's");
            Assert.False(CompiledSources.IsPackageOwned(templatesSource, root), "a source in templates was read as a package's");

            File.WriteAllText(Path.Combine(scratch, ".nupkg.metadata"), "{}");
            Assert.False(CompiledSources.IsPackageOwned(treeSource, root), "a sentinel at the root claimed a source of the tree");
        }
        finally
        {
            Directory.Delete(scratch, recursive: true);
        }
    }

    private static string WriteSource(string file)
    {
        _ = Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        File.WriteAllText(file, "// scratch\n");
        return file;
    }
}
