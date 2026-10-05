using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using Xunit;

namespace DotNetDifferentialEvolution.Protocol.Tests;

/// <summary>
/// No-suppression guard: a diagnostic is fixed or the rule is changed by a decision, never silenced. Tree-wide, not per
/// node: a suppression anywhere defeats the decision, so every file under the root is read, the skipped directories of
/// <see cref="Tree"/> excepted. Read as text, not as syntax (no Roslyn dependency), so a forbidden form written inside a
/// string literal or a comment that starts a line is reported too; the patterns are anchored to keep that rare.
/// </summary>
public sealed partial class NoSuppressionGuardTests
{
    /// <summary>Severities below warning; <c>default</c> counts as unverified (it may resolve to any SDK default below
    /// warning), except under <see cref="ProtocolConfig.DefaultScopedSeverityRules"/>.</summary>
    private static readonly string[] SeveritiesBelowWarning = ["none", "silent", "suggestion", "refactoring", "default"];

    /// <summary>The <c>NoWarn</c> tokens that are not a suppression: the SDK's own binding-redirect defaults and the
    /// reference to the inherited value.</summary>
    private static readonly HashSet<string> AllowedNoWarn = new(StringComparer.OrdinalIgnoreCase) { "1701", "1702", "CS1701", "CS1702", "$(NoWarn)" };

    [GeneratedRegex(@"^\s*#\s*pragma\s+warning\s+disable\b", RegexOptions.Multiline)]
    private static partial Regex PragmaDisablePattern();

    [GeneratedRegex(@"^\s*#\s*nullable\s+disable\b", RegexOptions.Multiline)]
    private static partial Regex NullableDisablePattern();

    /// <summary>The attribute applied, in a bracket of its own or after a comma in one; the name is assembled so that this
    /// file does not match its own pattern.</summary>
    [GeneratedRegex(@"(?:\[|,)\s*(?:assembly\s*:|module\s*:)?\s*(?:[\w.]+\.)?(?:Unconditional)?" + "Suppress" + @"Message(?:Attribute)?\s*[(\],]", RegexOptions.None)]
    private static partial Regex SuppressionAttributePattern();

    [GeneratedRegex(@"(?:\[|,)\s*(?:[\w.]+\.)?" + "Generated" + @"Code(?:Attribute)?\s*\(", RegexOptions.None)]
    private static partial Regex GeneratedCodeAttributePattern();

    [GeneratedRegex(@"dotnet_diagnostic\.([A-Za-z0-9_]+)\.severity\s*=\s*(\S+)", RegexOptions.None)]
    private static partial Regex DiagnosticSeverityPattern();

    [GeneratedRegex(@"dotnet_analyzer_diagnostic\.(?:category-[A-Za-z0-9_]+\.)?severity\s*=\s*(\S+)", RegexOptions.None)]
    private static partial Regex CategorySeverityPattern();

    [GeneratedRegex(@"^[a-z0-9_.]+\s*=\s*[^:=\r\n]+:(\S+)\s*$", RegexOptions.Multiline | RegexOptions.IgnoreCase)]
    private static partial Regex OptionSeverityPattern();

    [GeneratedRegex(@"^\s*\[(.+)\]\s*$", RegexOptions.Multiline)]
    private static partial Regex SectionHeaderPattern();

    /// <summary>A test attribute's <c>Skip</c> argument, or theory data hidden from discovery; the names are assembled so
    /// that this file does not match its own patterns.</summary>
    [GeneratedRegex(@"\b(?:Fact|Theory|InlineData|MemberData|ClassData)\s*\([^)\n]*\b" + "Skip" + @"\s*=", RegexOptions.None)]
    private static partial Regex SkippedTestPattern();

    [GeneratedRegex(@"\b" + "DisableDiscovery" + @"Enumeration\s*=\s*true\b", RegexOptions.None)]
    private static partial Regex HiddenTheoryDataPattern();

    /// <summary>No C# file disables a warning or the nullable context with a directive.</summary>
    [Fact]
    public void NoSourceFileDisablesADiagnosticWithADirective()
    {
        var sources = Sources();
        var problems = new List<string>();
        foreach (var path in sources)
        {
            var text = File.ReadAllText(path);
            problems.AddRange(PragmaDisablePattern().Matches(text).Select(match => $"{Tree.Relative(path)}:{LineOf(text, match.Index)}: #pragma warning disable"));
            problems.AddRange(NullableDisablePattern().Matches(text).Select(match => $"{Tree.Relative(path)}:{LineOf(text, match.Index)}: #nullable disable"));
        }

        Assert.True(problems.Count == 0, string.Join("\n", problems));
    }

    /// <summary>No C# file applies a suppression attribute (plain or unconditional, <c>assembly:</c> and <c>module:</c>
    /// targets included), and no file is named <c>GlobalSuppressions.cs</c>.</summary>
    [Fact]
    public void NoSourceFileCarriesASuppressionAttribute()
    {
        var sources = Sources();
        var problems = new List<string>();
        foreach (var path in sources)
        {
            var text = File.ReadAllText(path);
            problems.AddRange(SuppressionAttributePattern().Matches(text).Select(match => $"{Tree.Relative(path)}:{LineOf(text, match.Index)}: a suppression attribute"));
            if (string.Equals(Path.GetFileName(path), "Global" + "Suppressions.cs", StringComparison.OrdinalIgnoreCase))
            {
                problems.Add($"{Tree.Relative(path)}: a global suppressions file");
            }
        }

        Assert.True(problems.Count == 0, string.Join("\n", problems));
    }

    /// <summary>No project, props or targets file sets <c>NoWarn</c> (element or attribute) beyond the SDK's own
    /// defaults, sets <c>WarningsNotAsErrors</c> or <c>CodeAnalysisRuleSet</c>; no <c>*.ruleset</c> and no
    /// <c>Directory.Build.rsp</c> exists.</summary>
    [Fact]
    public void NoBuildFileSuppressesADiagnostic()
    {
        var files = Tree.Files().ToList();
        var buildFiles = files.Where(path => HasExtension(path, ".csproj", ".props", ".targets")).ToList();
        Assert.True(buildFiles.Count > 0, "found nothing: no project, props or targets file under the tree root, so the walk proves nothing");
        var problems = buildFiles.SelectMany(BuildFileProblems).ToList();
        problems.AddRange(files.Where(path => HasExtension(path, ".ruleset")).Select(path => $"{Tree.Relative(path)}: a rule-set file"));
        problems.AddRange(files.Where(path => string.Equals(Path.GetFileName(path), "Directory.Build.rsp", StringComparison.OrdinalIgnoreCase))
            .Select(path => $"{Tree.Relative(path)}: a response file can pass /nowarn to every build"));
        Assert.True(problems.Count == 0, string.Join("\n", problems));
    }

    /// <summary>No <c>.editorconfig</c> or <c>.globalconfig</c> sets a severity below warning, in any of the three forms a
    /// line can carry one. Raising a severity is not a suppression; lowering one is.</summary>
    [Fact]
    public void NoAnalyzerConfigurationLowersASeverityBelowWarning()
    {
        var sources = Sources();
        Assert.True(sources.Count > 0, "found nothing: the walk read no file, so it proves nothing");
        var problems = new List<string>();
        foreach (var path in AnalyzerConfigs())
        {
            problems.AddRange(FindLoweredSeverities(File.ReadAllText(path)).Select(lowered => $"{Tree.Relative(path)}: {lowered}"));
        }

        Assert.True(problems.Count == 0, "these analyzer-configuration lines set a severity below warning:\n" + string.Join("\n", problems));
    }

    /// <summary>
    /// No test is skipped and no theory data is hidden from discovery (this tree's addition to the kit, 2026-10-03): a
    /// skipped test is a red result silenced, which the root <c>BOOT.md</c> forbids like any other suppression.
    /// </summary>
    [Fact]
    public void NoTestIsSkippedOrHiddenFromDiscovery()
    {
        var sources = Sources();
        var problems = new List<string>();
        foreach (var path in sources)
        {
            var text = File.ReadAllText(path);
            problems.AddRange(SkippedTestPattern().Matches(text).Select(match => $"{Tree.Relative(path)}:{LineOf(text, match.Index)}: a skipped test"));
            problems.AddRange(HiddenTheoryDataPattern().Matches(text).Select(match => $"{Tree.Relative(path)}:{LineOf(text, match.Index)}: theory data hidden from discovery"));
        }

        Assert.True(problems.Count == 0, string.Join("\n", problems));
    }

    /// <summary>The skip check is not vacuously green: each form it must catch is caught.</summary>
    [Fact]
    public void TheSkipCheckCatchesASkippedTestAndHiddenData()
    {
        _ = Assert.Single(SkippedTestPattern().Matches("[Fact(" + "Skip" + " = \"flaky\")]"));
        _ = Assert.Single(SkippedTestPattern().Matches("[Theory(DisplayName = \"x\", " + "Skip" + " = \"later\")]"));
        _ = Assert.Single(HiddenTheoryDataPattern().Matches("[MemberData(nameof(Cases), " + "DisableDiscovery" + "Enumeration = true)]"));
        Assert.Empty(SkippedTestPattern().Matches("[Fact]"));
        Assert.Empty(SkippedTestPattern().Matches("[Trait(\"Category\", \"Gpu\")]"));
    }

    /// <summary>The lowering check is not vacuously green (AGENTS.md §13): each shape it must catch is caught, and each
    /// shape it must pass passes.</summary>
    [Fact]
    public void TheLoweringCheckCatchesADeliberatelyLoweredSeverity()
    {
        _ = Assert.Single(FindLoweredSeverities("dotnet_diagnostic.CA1000.severity = silent"));
        _ = Assert.Single(FindLoweredSeverities("dotnet_analyzer_diagnostic.category-Style.severity = suggestion"));
        _ = Assert.Single(FindLoweredSeverities("dotnet_analyzer_diagnostic.severity = none"));
        _ = Assert.Single(FindLoweredSeverities("csharp_style_var_elsewhere = true:none"));
        _ = Assert.Single(FindLoweredSeverities("[*.cs]\ndotnet_diagnostic.IDE0005.severity = default"));
        Assert.Empty(FindLoweredSeverities("csharp_style_var_elsewhere = true:warning"));
        Assert.Empty(FindLoweredSeverities("dotnet_diagnostic.CA1000.severity = error"));
        Assert.Empty(FindLoweredSeverities("dotnet_sort_system_directives_first = true"));
    }

    /// <summary>No source carries a generated-code marker (a generated file name, an auto-generated header comment, the
    /// generated-code attribute) and no analyzer configuration sets <c>generated_code</c>: each makes the analyzers, and
    /// <see cref="TypeShape.IsCompilerGenerated"/>, skip code an author wrote.</summary>
    [Fact]
    public void NoSourceFileCarriesAGeneratedCodeMarker()
    {
        var sources = Sources();
        var problems = new List<string>();
        foreach (var path in sources)
        {
            var name = Path.GetFileName(path);
            if (IsGeneratedFileName(name))
            {
                problems.Add($"{Tree.Relative(path)}: a file name the compiler treats as generated");
            }

            var text = File.ReadAllText(path);
            if (HasGeneratedHeader(text))
            {
                problems.Add($"{Tree.Relative(path)}: an auto-generated header comment");
            }

            problems.AddRange(GeneratedCodeAttributePattern().Matches(text).Select(match => $"{Tree.Relative(path)}:{LineOf(text, match.Index)}: a generated-code attribute"));
        }

        foreach (var path in AnalyzerConfigs())
        {
            var lines = File.ReadAllLines(path);
            for (var index = 0; index < lines.Length; index++)
            {
                if (lines[index].TrimStart().StartsWith("generated_code", StringComparison.OrdinalIgnoreCase))
                {
                    problems.Add($"{Tree.Relative(path)}:{index + 1}: a generated_code key");
                }
            }
        }

        Assert.True(problems.Count == 0, string.Join("\n", problems));
    }

    /// <summary>The root <c>Directory.Build.props</c> keeps every property of
    /// <see cref="ProtocolConfig.RequiredRootBuildProperties"/> at a matching value: the analyzer decision cannot be lost
    /// silently. Checks nothing while the dictionary is empty (the default).</summary>
    [Fact]
    public void TheRootBuildKeepsTheRequiredProperties()
    {
        if (ProtocolConfig.RequiredRootBuildProperties.Count == 0)
        {
            return;
        }

        var path = Path.Combine(Tree.Root, "Directory.Build.props");
        Assert.True(File.Exists(path), $"{Tree.Relative(path)} is missing, and ProtocolConfig.RequiredRootBuildProperties names properties it must keep");
        var elements = XDocument.Load(path).Descendants().ToList();
        var problems = new List<string>();
        foreach (var (property, pattern) in ProtocolConfig.RequiredRootBuildProperties)
        {
            var values = elements.Where(element => element.Name.LocalName == property).Select(element => element.Value.Trim()).ToList();
            if (values.Count == 0 || !values.All(value => Regex.IsMatch(value, pattern)))
            {
                problems.Add($"{Tree.Relative(path)}: {property} is [{string.Join(", ", values)}], required to match {pattern}");
            }
        }

        Assert.True(problems.Count == 0, string.Join("\n", problems));
    }

    private static List<string> Sources()
    {
        var sources = Tree.Files().Where(path => HasExtension(path, ".cs")).ToList();
        Assert.True(sources.Count > 0, "found nothing: no C# file under the tree root, so the walk proves nothing");
        return sources;
    }

    private static IEnumerable<string> AnalyzerConfigs() =>
        Tree.Files().Where(path => string.Equals(Path.GetFileName(path), ".editorconfig", StringComparison.OrdinalIgnoreCase) || HasExtension(path, ".globalconfig"));

    private static bool HasExtension(string path, params string[] extensions) =>
        extensions.Any(extension => path.EndsWith(extension, StringComparison.OrdinalIgnoreCase));

    private static int LineOf(string text, int index) => text.AsSpan(0, index).Count('\n') + 1;

    private static IEnumerable<string> BuildFileProblems(string path)
    {
        XDocument document;
        try
        {
            document = XDocument.Load(path);
        }
        catch (XmlException e)
        {
            return [$"{Tree.Relative(path)}: not well-formed XML ({e.Message})"];
        }

        var problems = new List<string>();
        foreach (var element in document.Descendants())
        {
            var name = element.Name.LocalName;
            if (name == "NoWarn")
            {
                problems.AddRange(DisallowedNoWarn(element.Value).Select(code => $"{Tree.Relative(path)}: <NoWarn> suppresses {code}"));
            }
            else if (name is "WarningsNotAsErrors" or "CodeAnalysisRuleSet")
            {
                problems.Add($"{Tree.Relative(path)}: sets {name} to '{element.Value.Trim()}'");
            }

            if (element.Attribute("NoWarn") is { } attribute)
            {
                problems.AddRange(DisallowedNoWarn(attribute.Value).Select(code => $"{Tree.Relative(path)}: a NoWarn attribute on {name} suppresses {code}"));
            }
        }

        return problems;
    }

    private static IEnumerable<string> DisallowedNoWarn(string value) =>
        value.Split([';', ',', ' ', '\n', '\r', '\t'], StringSplitOptions.RemoveEmptyEntries).Where(code => !AllowedNoWarn.Contains(code.Trim()));

    private static bool IsGeneratedFileName(string name) =>
        name.EndsWith(".g.cs", StringComparison.OrdinalIgnoreCase) || name.EndsWith(".g.i.cs", StringComparison.OrdinalIgnoreCase)
        || name.EndsWith(".generated.cs", StringComparison.OrdinalIgnoreCase) || name.EndsWith(".designer.cs", StringComparison.OrdinalIgnoreCase)
        || name.StartsWith("TemporaryGeneratedFile_", StringComparison.OrdinalIgnoreCase);

    /// <summary>Whether a comment of the leading trivia (the lines before the first line of code) names the
    /// auto-generated marker, in either spelling Roslyn recognises.</summary>
    private static bool HasGeneratedHeader(string text)
    {
        foreach (var raw in text.ReplaceLineEndings("\n").Split('\n'))
        {
            var line = raw.Trim();
            if (line.Length == 0)
            {
                continue;
            }

            if (!(line.StartsWith("//", StringComparison.Ordinal) || line.StartsWith("/*", StringComparison.Ordinal) || line.StartsWith('*')))
            {
                return false;
            }

            if (line.Contains("<auto" + "-generated", StringComparison.OrdinalIgnoreCase) || line.Contains("<auto" + "generated", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Every line of an analyzer configuration that sets a severity below warning, by rule, by category or
    /// globally, or by the <c>option = value:severity</c> suffix. A rule of <see cref="ProtocolConfig.DefaultScopedSeverityRules"/>
    /// may stay at <c>default</c> in a section that is not path-scoped, when a path-scoped section raises it.</summary>
    private static List<string> FindLoweredSeverities(string text)
    {
        var sections = SplitSections(text);
        var raisedInNarrowerSection = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (_, body) in sections.Where(section => IsPathScoped(section.Glob)))
        {
            foreach (Match match in DiagnosticSeverityPattern().Matches(body))
            {
                if (!IsBelowWarning(match.Groups[2].Value))
                {
                    _ = raisedInNarrowerSection.Add(match.Groups[1].Value.Trim());
                }
            }
        }

        var lowered = new List<string>();
        foreach (var (glob, body) in sections)
        {
            foreach (Match match in DiagnosticSeverityPattern().Matches(body))
            {
                var rule = match.Groups[1].Value.Trim();
                var severity = match.Groups[2].Value.Trim();
                var scopedDefault = !IsPathScoped(glob) && string.Equals(severity, "default", StringComparison.OrdinalIgnoreCase)
                    && ProtocolConfig.DefaultScopedSeverityRules.Contains(rule) && raisedInNarrowerSection.Contains(rule);
                if (IsBelowWarning(severity) && !scopedDefault)
                {
                    lowered.Add(match.Value.Trim());
                }
            }
        }

        lowered.AddRange(CategorySeverityPattern().Matches(text).Where(match => IsBelowWarning(match.Groups[1].Value)).Select(match => match.Value.Trim()));
        lowered.AddRange(OptionSeverityPattern().Matches(text).Where(match => IsBelowWarning(match.Groups[1].Value)).Select(match => match.Value.Trim()));
        return lowered;
    }

    private static bool IsBelowWarning(string severity) => SeveritiesBelowWarning.Contains(severity.Trim(), StringComparer.OrdinalIgnoreCase);

    /// <summary>A glob restricted to a path (<c>src/**.cs</c>), as opposed to one applying tree-wide (<c>*.cs</c>, or the
    /// file's top before any header).</summary>
    private static bool IsPathScoped(string glob) => glob.Contains('/', StringComparison.Ordinal);

    /// <summary>The sections of an analyzer configuration: text before the first header is one section with an empty glob.</summary>
    private static List<(string Glob, string Body)> SplitSections(string text)
    {
        var headers = SectionHeaderPattern().Matches(text);
        var sections = new List<(string Glob, string Body)>();
        if (headers.Count == 0)
        {
            sections.Add((string.Empty, text));
            return sections;
        }

        if (headers[0].Index > 0)
        {
            sections.Add((string.Empty, text[..headers[0].Index]));
        }

        for (var i = 0; i < headers.Count; i++)
        {
            var bodyStart = headers[i].Index + headers[i].Length;
            var bodyEnd = i + 1 < headers.Count ? headers[i + 1].Index : text.Length;
            sections.Add((headers[i].Groups[1].Value.Trim(), text[bodyStart..bodyEnd]));
        }

        return sections;
    }
}
