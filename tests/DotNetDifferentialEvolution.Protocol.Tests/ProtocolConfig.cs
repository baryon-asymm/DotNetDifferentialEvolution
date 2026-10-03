namespace ProtocolChecks;

/// <summary>
/// Everything tree-specific the reflection checks read, in one place. Every other file of this kit is generic and
/// should be dropped into a tree unchanged; a tree adapts the kit by editing this file alone. Nothing here names a
/// node of any particular tree except <see cref="NamespaceExceptions"/>, whose one default entry is the kit's own
/// test node and is explained there.
/// </summary>
internal static class ProtocolConfig
{
    /// <summary>The root namespace of the tree: a node's namespace is this, followed by the directory path from the
    /// tree root with the <see cref="TransparentSegments"/> dropped (AGENTS.md §1). The one place the name is written.
    /// <para>Empty in this tree: its top directories already carry full dotted names
    /// (<c>src/DotNetDifferentialEvolution.GPU</c>), so the path alone is the namespace and the root node, which holds no
    /// code, owns none (see <see cref="Node.Namespace"/>; a declared deviation of the kit, the node's BOOT.md).</para></summary>
    public const string RootNamespace = "";

    /// <summary>Path segments that are grouping directories, not part of any namespace (AGENTS.md §1: `src/`, `tests/`
    /// are not nodes and the namespace reads through them). <c>src/Orders</c> is <c>RootNamespace.Orders</c>.</summary>
    public static readonly IReadOnlySet<string> TransparentSegments = new HashSet<string>(StringComparer.Ordinal)
    {
        "src", "tests", "samples", "benchmarks",
    };

    /// <summary>Path prefixes (from the tree root, '/' separated, trailing slash included) of the library source nodes:
    /// what <see cref="Node.IsSource"/> means. Read by the optional coupling facts and offered to forbidden-call rules
    /// as a scope; the mandatory facts do not depend on it (they tell libraries from tests by the xunit reference).</summary>
    public static readonly IReadOnlyList<string> SourcePrefixes = ["src/"];

    /// <summary>Path prefixes of the test nodes: what <see cref="Node.IsTest"/> means.</summary>
    public static readonly IReadOnlyList<string> TestPrefixes = ["tests/"];

    /// <summary>Directory names never read as nodes or as source, beyond every directory whose name starts with a dot
    /// (a tool's cache or state; <see cref="DotDirectoriesRead"/> excepted). The first group repeats the protocol
    /// linter's own default exclusions, so that the linter and the reflection checks see the same set of nodes; the
    /// names in <see cref="LinterExcludes"/> are added to this set and passed to the linter, one list for both.</summary>
    public static readonly IReadOnlySet<string> SkippedDirectories = new HashSet<string>(StringComparer.Ordinal)
    {
        "artifacts", "bin", "build", "coverage", "dist", "env", "node_modules", "obj",
        "out", "packages", "target", "TestResults", "vendor", "venv", "__pycache__",
    };

    /// <summary>Dot-directories that are committed configuration and are read like any other directory (the linter's
    /// own exception).</summary>
    public static readonly IReadOnlySet<string> DotDirectoriesRead = new HashSet<string>(StringComparer.Ordinal) { ".github" };

    /// <summary>Extra directory names skipped by both the linter (<c>--exclude</c>) and the walk here: <c>templates</c>
    /// when the protocol kit's own document templates live in the tree. Empty by default.</summary>
    public static readonly IReadOnlyList<string> LinterExcludes = ["templates"];

    /// <summary>The protocol linter, relative to the tree root (AGENTS.md §13: it lives in the tree as a node of its own).</summary>
    public const string LinterPath = "tools/protocol-lint/protocol_lint.py";

    /// <summary>The Python executable the lint fact starts; must be on PATH (3.8 or later).</summary>
    public const string PythonExecutable = "python";

    /// <summary>The linter's arguments after the script and the tree root. <c>--strict</c>: a warning fails too, the
    /// tree's criterion is zero warnings. <see cref="LinterExcludes"/> are appended as <c>--exclude</c>.</summary>
    public static readonly IReadOnlyList<string> LinterArguments = ["--strict"];

    /// <summary>How long the lint fact waits for the linter.</summary>
    public static readonly TimeSpan LinterTimeout = TimeSpan.FromMinutes(2);

    /// <summary>The directory, relative to the tree root, that holds the approved snapshots
    /// (<c>PublicSurface.approved.txt</c>, <c>TreeContract.approved.txt</c>) and receives the <c>.actual.txt</c> files.
    /// Normally the directory of this test node itself.</summary>
    public const string SnapshotDirectory = "tests/DotNetDifferentialEvolution.Protocol.Tests";

    /// <summary>Whether the tree-contract snapshot fact runs (<see cref="TreeContractSnapshotTests"/>). Off by default:
    /// a tree turns it on once some <c>API.md</c> carries a ✅ section whose heading holds <see cref="TreeContractMarker"/>.</summary>
    public static bool TreeContractSnapshot => false;

    /// <summary>The text that marks an <c>API.md</c> section heading (a line starting <c>## </c>) as a tree contract:
    /// internal types offered to friend assemblies of the same tree rather than the package surface.</summary>
    public const string TreeContractMarker = "(tree contract)";

    /// <summary>The nodes a tree treats as numerical, as path prefixes: a listed path covers its descendants too, so a
    /// future child node cannot slip out from under the list (the lesson of a hand-typed list that a new child left).
    /// Read only by forbidden-call rules a tree writes itself (<see cref="IsNumerical"/>). Every entry must be a node
    /// (<see cref="ConfigTests"/>). Empty by default.</summary>
    public static readonly IReadOnlyList<string> NumericalNodes = [];

    /// <summary>A node whose code lives in a namespace other than the one its path gives, keyed by the node's path, with
    /// the namespace it uses: a declared deviation of AGENTS.md §1 (§12). The node itself must say so in its own BOOT.md;
    /// the namespace fact skips the listed namespace in that node's own assembly and fails once no type uses it any
    /// more (a stale entry is a deviation that has been lifted and must be removed).
    /// <para>The default entry is this kit itself: its files keep the neutral namespace <c>ProtocolChecks</c> so they can
    /// be dropped in unchanged. A tree that renames them to <c>RootNamespace.Protocol.Tests</c> deletes the entry, and the
    /// staleness check makes it do so.</para></summary>
    public static readonly IReadOnlyDictionary<string, string> NamespaceExceptions = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["tests/DotNetDifferentialEvolution.Protocol.Tests"] = "ProtocolChecks",
    };

    /// <summary>Rules against particular calls, read by <see cref="ForbiddenCallTests"/> through <see cref="ForbiddenCalls"/>.
    /// Empty by default; the fact then checks nothing and says so in its name only. A neutral example, forbidding console
    /// output in library code (the caller allowed to write is listed by its <c>Namespace.Type.Method</c>):
    /// <code>
    /// new ForbiddenCallRule(
    ///     Name: "no console output in library nodes",
    ///     Scope: node =&gt; node.IsSource,
    ///     Callee: callee =&gt; callee.DeclaringType == typeof(Console) &amp;&amp; callee.Name == "WriteLine",
    ///     Signature: _ =&gt; true,
    ///     AllowList: new HashSet&lt;string&gt;(StringComparer.Ordinal) { "MyTree.Cli.Program.Main" }),
    /// </code></summary>
    public static readonly IReadOnlyList<ForbiddenCallRule> ForbiddenCallRules =
    [
        // The packages are libraries: they report through return values, exceptions and the observer hook, never by
        // printing. No caller is allowed.
        new ForbiddenCallRule(
            Name: "no console output in library nodes",
            Scope: node => node.IsSource,
            Callee: callee => callee.DeclaringType == typeof(Console),
            Signature: _ => true,
            AllowList: new HashSet<string>(StringComparer.Ordinal)),
    ];

    /// <summary>The largest number of distinct tree types one outermost type of a <see cref="Node.IsSource"/> node may name
    /// in its signatures and method bodies (efferent coupling, Ce). Null: the fact does not measure. The figure is a
    /// project's choice (one tree uses 14), not the protocol's.</summary>
    public static int? MaxEfferentCouplingPerType => null;

    /// <summary>Whether the stable-dependencies rule runs: over the declared dependencies of the source nodes that hold a
    /// project, I = Ce / (Ca + Ce) never rises along a dependency. Off by default.</summary>
    public static bool StableDependencies => false;

    /// <summary>Rule IDs an <c>.editorconfig</c> may hold at <c>default</c> in a tree-wide section, provided a path-scoped
    /// section raises the same rule to warning or above (one tree raises IDE0005 in <c>src/**.cs</c> only, where a
    /// documentation file is generated). Empty by default: every <c>default</c> reads as a lowering.</summary>
    public static readonly IReadOnlySet<string> DefaultScopedSeverityRules = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    /// <summary>MSBuild properties the root <c>Directory.Build.props</c> must keep, each with a regular expression its value
    /// must match (for instance <c>["TreatWarningsAsErrors"] = "^true$"</c>, <c>["AnalysisLevel"] = "^latest-all$"</c>):
    /// the analyzer decision a tree made, held by a fact so it cannot be lost silently. Empty by default.</summary>
    public static readonly IReadOnlyDictionary<string, string> RequiredRootBuildProperties = new Dictionary<string, string>(StringComparer.Ordinal);

    /// <summary>Whether a node falls under <see cref="NumericalNodes"/>: listed itself, or a descendant of a listed node.</summary>
    public static bool IsNumerical(Node node)
    {
        ArgumentNullException.ThrowIfNull(node);
        foreach (var path in NumericalNodes)
        {
            if (node.RelativePath == path || node.RelativePath.StartsWith(path + "/", StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }
}
