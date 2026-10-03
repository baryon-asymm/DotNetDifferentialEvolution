using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;

namespace ProtocolChecks;

/// <summary>
/// The C# sources the tree's assemblies were actually compiled from, read from each assembly's portable PDB document
/// table: a directory walk with name exclusions misses a source the SDK still compiles because it sits in a directory
/// the walk skips, while the compiler's own record cannot miss it. Needs a portable PDB beside every DLL (the SDK
/// default) and unmapped source paths (no <c>PathMap</c>, no <c>ContinuousIntegrationBuild</c> during tests).
/// </summary>
internal static class CompiledSources
{
    private static readonly Lazy<IReadOnlyList<string>> AllLazy = new(LoadAll);

    /// <summary>Every compiled <c>*.cs</c> path under the tree root, across every assembly of the tree, deduplicated and
    /// ordinal; build output and restored package sources excluded.</summary>
    public static IReadOnlyList<string> All => AllLazy.Value;

    /// <summary>The <c>*.cs</c> paths one assembly was compiled from, from its portable PDB.</summary>
    public static IReadOnlyList<string> Of(Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(assembly);
        using var provider = OpenPdb(assembly);
        var reader = provider.GetMetadataReader();
        var paths = new List<string>();
        var root = EnsureTrailingSeparator(Path.GetFullPath(Tree.Root));
        foreach (var handle in reader.Documents)
        {
            var name = reader.GetString(reader.GetDocument(handle).Name);
            if (!name.EndsWith(".cs", StringComparison.OrdinalIgnoreCase) || !Path.IsPathFullyQualified(name))
            {
                continue;
            }

            var full = Path.GetFullPath(name);
            if (full.StartsWith(root, StringComparison.OrdinalIgnoreCase) && !IsBuildOutput(full) && !IsPackageOwned(full, root))
            {
                paths.Add(full);
            }
        }

        return paths;
    }

    /// <summary>Whether a compiled source belongs to a restored NuGet package rather than to the tree: an ancestor
    /// directory below the root holds the <c>*.nupkg.metadata</c> sentinel NuGet writes into every package folder (a
    /// package cache kept inside the workspace, as CI often does). A sentinel at the root itself never claims a source.</summary>
    /// <param name="path">The full path of a compiled source under <paramref name="root"/>.</param>
    /// <param name="root">The tree root with a trailing separator; the upward search stops below it.</param>
    /// <summary>The assembly's portable PDB: the file beside the DLL (the SDK default), or, failing that, the one embedded
    /// in the DLL (<c>DebugType embedded</c>, which a package shipping SourceLink uses). Adapted in this tree: the kit
    /// read only the file beside the DLL.</summary>
    private static MetadataReaderProvider OpenPdb(Assembly assembly)
    {
        var pdbPath = Path.ChangeExtension(assembly.Location, ".pdb");
        if (assembly.Location.Length > 0 && File.Exists(pdbPath))
        {
            using var stream = File.OpenRead(pdbPath);
            return MetadataReaderProvider.FromPortablePdbStream(stream, MetadataStreamOptions.PrefetchMetadata);
        }

        if (assembly.Location.Length > 0)
        {
            using var peReader = new PEReader(File.OpenRead(assembly.Location));
            foreach (var entry in peReader.ReadDebugDirectory())
            {
                if (entry.Type == DebugDirectoryEntryType.EmbeddedPortablePdb)
                {
                    return peReader.ReadEmbeddedPortablePdbDebugDirectoryData(entry);
                }
            }
        }

        throw new InvalidOperationException($"{assembly.GetName().Name} carries no portable PDB, beside its DLL at {assembly.Location} or embedded in it; " +
                                            "the compiled-source fact reads a node's sources from it (DebugType portable or embedded)");
    }

    internal static bool IsPackageOwned(string path, string root)
    {
        ArgumentNullException.ThrowIfNull(root);
        var directory = Path.GetDirectoryName(path);
        while (directory is not null && EnsureTrailingSeparator(directory).Length > root.Length)
        {
            if (Directory.EnumerateFiles(directory, "*.nupkg.metadata").Any())
            {
                return true;
            }

            directory = Path.GetDirectoryName(directory);
        }

        return false;
    }

    /// <summary>Under a <c>bin</c> or <c>obj</c> directory: build output, a source generator's emitted files included.</summary>
    private static bool IsBuildOutput(string path) =>
        path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).Any(segment => segment is "bin" or "obj");

    private static string EnsureTrailingSeparator(string path) =>
        path.EndsWith(Path.DirectorySeparatorChar) ? path : path + Path.DirectorySeparatorChar;

    private static List<string> LoadAll()
    {
        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var assembly in NodeAssemblies.Assemblies.Values.Distinct())
        {
            paths.UnionWith(Of(assembly));
        }

        return [.. paths.Order(StringComparer.Ordinal)];
    }
}
