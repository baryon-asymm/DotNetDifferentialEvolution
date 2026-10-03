using System.Text.RegularExpressions;

namespace DotNetDifferentialEvolution.Protocol.Tests;

/// <summary>
/// The grammar of an <c>API.md</c>: its ✅ C# blocks and the declarations in them, and the text that sits under ✅ more
/// broadly (AGENTS.md §7). The one meaning of "named in the API.md" for <see cref="DeclarationTests"/> (a declaration
/// exists, read from the ✅ C# blocks) and <see cref="CoverageTests"/> (every exported type is at least named, in prose
/// or in code, under a ✅ mark). A type need not be given a full declaration: one public only because a framework
/// requires it may be named in a sentence, and that still counts.
/// </summary>
internal static partial class ApiDeclarations
{
    [GeneratedRegex(
        @"\b(?:record\s+struct|record\s+class|record|class|struct|enum|interface|delegate\s+[\w<>\[\],.?]+)\s+(\w+)", RegexOptions.Compiled)]
    private static partial Regex TypeRegex();

    [GeneratedRegex(@"\b(\w+)\s*\{\s*(?:get|set|init)", RegexOptions.Compiled)]
    private static partial Regex PropertyRegex();

    [GeneratedRegex(@"\b(\w+)\s*(?:<[\w,\s]+>)?\s*\(", RegexOptions.Compiled)]
    private static partial Regex MethodRegex();

    [GeneratedRegex(
        @"^\s*(?:(?:public|internal|private|protected|static|readonly|const|required|new|volatile|unsafe)\s+)*[\w.]+(?:<[^;=]*>)?(?:\[[\s,]*\])*\??\s+(?<names>\w+(?:\s*=\s*[^,;]+)?(?:\s*,\s*\w+(?:\s*=\s*[^,;]+)?)*)\s*;\s*$",
        RegexOptions.Compiled)]
    private static partial Regex FieldDeclarationRegex();

    [GeneratedRegex(@"^\s*(\w+)", RegexOptions.Compiled)]
    private static partial Regex FieldNameRegex();

    [GeneratedRegex(@"^\s*(\w+)\s*(?:=\s*[^,]+?)?\s*,?\s*$", RegexOptions.Compiled)]
    private static partial Regex EnumMemberRegex();

    [GeneratedRegex(@"(\w+)\s*$", RegexOptions.Compiled)]
    private static partial Regex ParameterNameRegex();

    private static readonly HashSet<string> Keywords = new(StringComparer.Ordinal)
    {
        "if", "for", "foreach", "while", "switch", "return", "new", "get", "set", "init", "throw",
        "using", "nameof", "typeof", "default", "sizeof", "var", "operator", "where", "else", "do", "in",

        // A modifier immediately before a parenthesis is a method whose return type is a tuple
        // (`internal static (long Low, long High) Band(...)`), not a member named after the modifier.
        "public", "internal", "private", "protected", "static", "readonly", "sealed",
        "override", "virtual", "abstract", "async", "extern", "unsafe", "partial",
        "ref", "out", "params", "delegate", "record", "class", "struct", "enum", "interface",
    };

    /// <summary>Whether a type of the given simple name is named anywhere in the document's ✅-marked text (prose or
    /// code; a document without a single status mark counts as ✅ throughout).</summary>
    public static bool NamesType(string document, string simpleName) =>
        Mentions(Classify(document).Where(line => line.Implemented).Select(line => line.Text), simpleName);

    /// <summary>Whether the name is named in ✅ text outside every tree-contract section: the package surface.</summary>
    public static bool NamesTypeInPackageSurface(string document, string simpleName) =>
        Mentions(Classify(document).Where(line => line.Implemented && !line.TreeContract).Select(line => line.Text), simpleName);

    /// <summary>Whether the name is named in ✅ text of a section whose heading carries
    /// <see cref="ProtocolConfig.TreeContractMarker"/>.</summary>
    public static bool NamesTypeInTreeContract(string document, string simpleName) =>
        Mentions(Classify(document).Where(line => line.Implemented && line.TreeContract).Select(line => line.Text), simpleName);

    /// <summary>The C# blocks of a document whose nearest status mark above is ✅ (or that have no mark above at all).</summary>
    public static IEnumerable<string> ImplementedCsharpBlocks(string document) => ImplementedCsharpBlocksBySection(document).Select(pair => pair.Block);

    /// <summary>The type names a document's ✅ C# blocks declare, each with whether its section is a tree contract.</summary>
    public static IEnumerable<(string Name, bool TreeContract)> DeclaredTypeSections(string document) =>
        ImplementedCsharpBlocksBySection(document).SelectMany(pair => Declarations(pair.Block).Where(d => d.IsType).Select(d => (d.Name, pair.TreeContract)));

    /// <summary>
    /// The names a C# block declares: types (class, struct, record, enum, interface, delegate), the positional
    /// parameters of records, properties, methods, fields (several per line), enum members. Lines inside an open
    /// parameter list are skipped. Each declaration kind is tried in turn on every line; the first that matches wins.
    /// </summary>
    public static IEnumerable<Declaration> Declarations(string block)
    {
        ArgumentNullException.ThrowIfNull(block);
        var lines = block.Split('\n');
        for (var index = 0; index < lines.Length; index++)
        {
            var line = StripComment(lines[index]);
            if (IsSkippable(line))
            {
                continue;
            }

            foreach (var declaration in OnLine(lines, ref index, line))
            {
                yield return declaration;
            }
        }
    }

    private static bool Mentions(IEnumerable<string> lines, string simpleName) =>
        Regex.IsMatch(string.Join("\n", lines), $@"\b{Regex.Escape(simpleName)}\b");

    private static IEnumerable<(string Block, bool TreeContract)> ImplementedCsharpBlocksBySection(string document)
    {
        var block = new List<string>();
        var blockImplemented = false;
        var blockTreeContract = false;
        var wasInsideBlock = false;
        foreach (var (text, implemented, insideBlock, treeContract) in Classify(document))
        {
            if (insideBlock)
            {
                block.Add(text);
                blockImplemented = implemented;
                blockTreeContract = treeContract;
                wasInsideBlock = true;
                continue;
            }

            if (wasInsideBlock)
            {
                if (blockImplemented)
                {
                    yield return (string.Join("\n", block), blockTreeContract);
                }

                block.Clear();
                wasInsideBlock = false;
            }
        }
    }

    /// <summary>
    /// Every line of the document, classified by two state machines. The status one (AGENTS.md §7): a ⏳ or ✅ outside a
    /// C# fence sets the state until the next mark, so the nearest mark above a block decides it; a mark-like character
    /// inside example code does not count. The tree-contract one: every <c>## </c> heading resets it, and it is on only
    /// under a heading carrying <see cref="ProtocolConfig.TreeContractMarker"/>. The fence lines themselves are outside.
    /// </summary>
    private static IEnumerable<(string Text, bool Implemented, bool InsideCSharpBlock, bool TreeContract)> Classify(string document)
    {
        ArgumentNullException.ThrowIfNull(document);
        var implemented = true;
        var inside = false;
        var treeContract = false;
        foreach (var line in document.ReplaceLineEndings("\n").Split('\n'))
        {
            if (line.StartsWith("```", StringComparison.Ordinal))
            {
                if (inside)
                {
                    inside = false;
                }
                else if (line.Contains("csharp", StringComparison.Ordinal) || line.TrimEnd() == "```cs")
                {
                    inside = true;
                }

                yield return (line, implemented, false, treeContract);
                continue;
            }

            if (!inside)
            {
                if (line.Contains('⏳', StringComparison.Ordinal))
                {
                    implemented = false;
                }
                else if (line.Contains('✅', StringComparison.Ordinal))
                {
                    implemented = true;
                }

                if (line.StartsWith("## ", StringComparison.Ordinal))
                {
                    treeContract = line.Contains(ProtocolConfig.TreeContractMarker, StringComparison.Ordinal);
                }
            }

            yield return (line, implemented, inside, treeContract);
        }
    }

    private static bool IsSkippable(string line)
    {
        var trimmed = line.TrimStart();
        return line.Length == 0 || trimmed.StartsWith("using ", StringComparison.Ordinal)
            || trimmed.StartsWith("namespace ", StringComparison.Ordinal) || trimmed.StartsWith('[');
    }

    /// <summary>The declarations of one line: a type first (which may open a parameter list spanning further lines),
    /// then a property, a method (which may open a parameter list too), a field list, or an enum member.</summary>
    private static List<Declaration> OnLine(string[] lines, ref int index, string line)
    {
        var type = TypeDeclaration(lines, ref index, line);
        if (type is not null)
        {
            return type;
        }

        var property = PropertyDeclaration(line);
        if (property is not null)
        {
            return [property.Value];
        }

        var method = MethodDeclaration(lines, ref index, line);
        if (method is not null)
        {
            return [method.Value];
        }

        var fields = FieldDeclarations(line);
        if (fields is not null)
        {
            return fields;
        }

        var enumMember = EnumMemberDeclaration(line);
        return enumMember is null ? [] : [enumMember.Value];
    }

    /// <summary>A type declaration and the positional parameters of a record, following the parameter list across
    /// lines. Whether the type opens a body decides who owns the members after it (<see cref="Declaration.OpensBody"/>).</summary>
    private static List<Declaration>? TypeDeclaration(string[] lines, ref int index, string line)
    {
        var match = TypeRegex().Match(line);
        if (!match.Success)
        {
            return null;
        }

        var declarations = new List<Declaration> { new(match.Groups[1].Value, IsType: true, IsEnumMember: false) };
        foreach (var parameter in ParameterList(lines, ref index, line))
        {
            declarations.Add(new Declaration(parameter, IsType: false, IsEnumMember: false) { FromTypeLine = true });
        }

        declarations[0] = declarations[0] with { OpensBody = OpensBodyAt(lines, index) };
        return declarations;
    }

    /// <summary>Whether the type declared at <paramref name="index"/> owns what follows it: its body must be left open at
    /// the end of the line, or open on the next non-blank line. A record closed by a semicolon and an enum whose whole
    /// body fits on one line own nothing beyond themselves.</summary>
    private static bool OpensBodyAt(string[] lines, int index)
    {
        var line = StripComment(lines[index]).TrimEnd();
        if (line.EndsWith(';'))
        {
            return false;
        }

        var open = line.Count(c => c == '{') - line.Count(c => c == '}');
        if (open > 0)
        {
            return true;
        }

        if (line.Contains('{', StringComparison.Ordinal))
        {
            return false;
        }

        for (var next = index + 1; next < lines.Length; next++)
        {
            var following = StripComment(lines[next]).Trim();
            if (following.Length > 0)
            {
                return following.StartsWith('{');
            }
        }

        return false;
    }

    private static Declaration? PropertyDeclaration(string line)
    {
        var match = PropertyRegex().Match(line);
        return match.Success ? new Declaration(match.Groups[1].Value, IsType: false, IsEnumMember: false) : null;
    }

    /// <summary>A method, constructor or operator declaration: the first name before a parenthesis that is not a keyword
    /// (a modifier before a tuple return type is passed over, and the method's own name found after it); skips over its
    /// parameter list when it spans further lines. The parameter list is followed from the method's own parenthesis,
    /// not from the start of the line: on a line where a tuple return type closes before the name
    /// (`    int High) Band(`), the tuple's ")" would otherwise cancel the name's "(" and the parameters on the next
    /// lines would be read as declarations of their own (`double p = 0.5);` as a field `p`). A tuple split across lines
    /// needs no joining before the name: the continuation line that carries the name is read as a declaration line.</summary>
    private static Declaration? MethodDeclaration(string[] lines, ref int index, string line)
    {
        var match = MethodRegex().Matches(line).FirstOrDefault(candidate => !Keywords.Contains(candidate.Groups[1].Value));
        if (match is null)
        {
            return null;
        }

        _ = Collect(lines, ref index, line[match.Index..]);
        return new Declaration(match.Groups[1].Value, IsType: false, IsEnumMember: false);
    }

    private static List<Declaration>? FieldDeclarations(string line)
    {
        var match = FieldDeclarationRegex().Match(line);
        if (!match.Success)
        {
            return null;
        }

        var declarations = new List<Declaration>();
        foreach (var declarator in match.Groups["names"].Value.Split(','))
        {
            var name = FieldNameRegex().Match(declarator);
            if (name.Success && !Keywords.Contains(name.Groups[1].Value))
            {
                declarations.Add(new Declaration(name.Groups[1].Value, IsType: false, IsEnumMember: false));
            }
        }

        return declarations;
    }

    private static Declaration? EnumMemberDeclaration(string line)
    {
        var match = EnumMemberRegex().Match(line);
        return match.Success && !Keywords.Contains(match.Groups[1].Value) ? new Declaration(match.Groups[1].Value, IsType: false, IsEnumMember: true) : null;
    }

    /// <summary>The parameter names of a positional record, following the list across lines; empty for a type without one.</summary>
    private static List<string> ParameterList(string[] lines, ref int index, string first)
    {
        var names = new List<string>();
        if (!first.Contains('(', StringComparison.Ordinal))
        {
            return names;
        }

        var text = Collect(lines, ref index, first);
        var open = text.IndexOf('(', StringComparison.Ordinal);
        var close = text.LastIndexOf(')');
        if (open < 0 || close <= open)
        {
            return names;
        }

        foreach (var parameter in text[(open + 1)..close].Split(','))
        {
            var withoutDefault = parameter.Split('=')[0].Trim();
            var name = ParameterNameRegex().Match(withoutDefault);
            if (name.Success && char.IsUpper(name.Groups[1].Value[0]))
            {
                names.Add(name.Groups[1].Value);
            }
        }

        return names;
    }

    /// <summary>The line and its continuation lines until the parentheses balance; advances <paramref name="index"/>.</summary>
    private static string Collect(string[] lines, ref int index, string first)
    {
        var text = first;
        var depth = Depth(first);
        while (depth > 0 && index + 1 < lines.Length)
        {
            index++;
            var next = StripComment(lines[index]);
            text += " " + next;
            depth += Depth(next);
        }

        return text;

        static int Depth(string line) => line.Count(c => c == '(') - line.Count(c => c == ')');
    }

    private static string StripComment(string line)
    {
        var trimmed = line.TrimStart();
        if (trimmed.StartsWith("//", StringComparison.Ordinal))
        {
            return string.Empty;
        }

        var comment = line.IndexOf("//", StringComparison.Ordinal);
        return (comment >= 0 ? line[..comment] : line).TrimEnd();
    }
}
