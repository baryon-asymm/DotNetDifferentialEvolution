using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

namespace ProtocolChecks;

/// <summary>
/// Where the tree is and what its nodes are: the root found upward from this source file (AGENTS.md §13: from the
/// source, never from the binary), the nodes found by directory path (a directory holding both documents), the
/// skipped directories taken from <see cref="ProtocolConfig"/>, and paths relative to the root.
/// </summary>
internal static class Tree
{
    private static readonly Regex AssemblyNameRegex = new(@"<AssemblyName>\s*([^<]+?)\s*</AssemblyName>", RegexOptions.Compiled);

    private static readonly Lazy<string> RootLazy = new(FindRoot);

    private static readonly Lazy<IReadOnlyList<Node>> NodesLazy = new(FindNodes);

    private static readonly Lazy<IReadOnlySet<string>> SkippedLazy = new(
        () => new HashSet<string>(ProtocolConfig.SkippedDirectories.Concat(ProtocolConfig.LinterExcludes), StringComparer.Ordinal));

    /// <summary>The directory holding AGENTS.md, found upward from this file. Needs the sources on disk where the tests
    /// run: a build that maps source paths (deterministic CI paths, <c>PathMap</c>) breaks it, and it says so.</summary>
    public static string Root => RootLazy.Value;

    /// <summary>Every node of the tree, ordered by path; the root first.</summary>
    public static IReadOnlyList<Node> Nodes => NodesLazy.Value;

    /// <summary>The directory names never walked: <see cref="ProtocolConfig.SkippedDirectories"/> and
    /// <see cref="ProtocolConfig.LinterExcludes"/>. Dot-directories are skipped by <see cref="IsSkipped"/>.</summary>
    public static IReadOnlySet<string> Skipped => SkippedLazy.Value;

    /// <summary>Whether a directory, by its own name, is never walked: a listed name, or a dot-directory that is not
    /// committed configuration (the linter's own rule, so both see the same tree).</summary>
    public static bool IsSkipped(string directory)
    {
        var name = Path.GetFileName(directory);
        return Skipped.Contains(name) || (name.StartsWith('.') && !ProtocolConfig.DotDirectoriesRead.Contains(name));
    }

    /// <summary>A path relative to the tree root, '/' separated, empty for the root itself.</summary>
    public static string Relative(string path)
    {
        var relative = Path.GetRelativePath(Root, path).Replace('\\', '/');
        return relative == "." ? string.Empty : relative;
    }

    /// <summary>Every file under the root whose directory chain contains no skipped directory.</summary>
    public static IEnumerable<string> Files()
    {
        var pending = new Stack<string>();
        pending.Push(Root);
        while (pending.Count > 0)
        {
            var directory = pending.Pop();
            foreach (var file in Directory.GetFiles(directory).OrderBy(file => file, StringComparer.Ordinal))
            {
                yield return file;
            }

            foreach (var child in Directory.GetDirectories(directory).Where(child => !IsSkipped(child)).OrderByDescending(child => child, StringComparer.Ordinal))
            {
                pending.Push(child);
            }
        }
    }

    /// <summary>The name the build gives a project's assembly: the project file's own name, unless the project overrides
    /// it with an explicit <c>&lt;AssemblyName&gt;</c> (a tool whose command is lower case, say). A property reference
    /// such as <c>$(MSBuildProjectName)</c> is read as the file name, which is what it evaluates to.</summary>
    internal static string AssemblyNameOf(string projectFile)
    {
        var fileName = Path.GetFileNameWithoutExtension(projectFile);
        var overridden = AssemblyNameRegex.Match(File.ReadAllText(projectFile));
        if (!overridden.Success)
        {
            return fileName;
        }

        var value = overridden.Groups[1].Value;
        return value.Contains("$(", StringComparison.Ordinal) ? value.Replace("$(MSBuildProjectName)", fileName, StringComparison.Ordinal) : value;
    }

    private static string FindRoot()
    {
        var directory = Path.GetDirectoryName(ThisFile());
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory, "AGENTS.md")))
            {
                return directory;
            }

            directory = Path.GetDirectoryName(directory);
        }

        throw new InvalidOperationException(
            "the tree root (a directory with AGENTS.md) was not found above " + ThisFile() +
            "; the checks find the root from their own source path ([CallerFilePath]), so the sources must be on disk " +
            "where the tests run and the build must not map source paths");
    }

    private static string ThisFile([CallerFilePath] string path = "") => path;

    private static List<Node> FindNodes()
    {
        var nodes = new List<Node>();
        Walk(Root);
        return [.. nodes.OrderBy(node => node.RelativePath, StringComparer.Ordinal)];

        void Walk(string directory)
        {
            if (File.Exists(Path.Combine(directory, "BOOT.md")) && File.Exists(Path.Combine(directory, "API.md")))
            {
                var projects = Directory.GetFiles(directory, "*.csproj");
                if (projects.Length > 1)
                {
                    throw new InvalidOperationException($"{Relative(directory)} holds {projects.Length} projects; a node has one assembly");
                }

                nodes.Add(new Node(Relative(directory), directory, projects.Length == 1 ? AssemblyNameOf(projects[0]) : null));
            }

            foreach (var child in Directory.GetDirectories(directory))
            {
                if (!IsSkipped(child))
                {
                    Walk(child);
                }
            }
        }
    }
}
