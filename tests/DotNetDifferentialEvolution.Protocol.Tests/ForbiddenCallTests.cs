using Xunit;

namespace ProtocolChecks;

/// <summary>
/// Optional, behind <see cref="ProtocolConfig.ForbiddenCallRules"/> (empty by default): every configured rule holds
/// over the own types of the nodes its scope accepts. The example in <see cref="ProtocolConfig.ForbiddenCallRules"/>
/// forbids console output in library nodes. The second fact proves the helper itself on a probe of this class, so the
/// frame has been seen red even in a tree that configures no rule (AGENTS.md §13).
/// </summary>
public sealed class ForbiddenCallTests
{
    /// <summary>No node in a rule's scope makes a call the rule forbids, and no allow-list entry is stale.</summary>
    [Fact]
    public void NoConfiguredForbiddenCallIsMade()
    {
        var problems = new List<string>();
        foreach (var rule in ProtocolConfig.ForbiddenCallRules)
        {
            var types = NodeAssemblies.CodeNodes.Where(rule.Scope).SelectMany(NodeAssemblies.TypesOf).ToList();
            var (found, calls) = ForbiddenCalls.Find(types, rule.Callee, rule.Signature, rule.AllowList);
            Assert.True(calls > 0, $"found nothing: the rule \"{rule.Name}\" walked no call in its scope, so it proves nothing");
            problems.AddRange(found.Select(problem => $"{rule.Name}: {problem}"));
        }

        Assert.True(problems.Count == 0, string.Join("\n", problems));
    }

    /// <summary>The helper reports a forbidden call with its caller, honours an allow-list entry, reports a stale one,
    /// and attributes a call made inside a lambda to the method that wrote it.</summary>
    [Fact]
    public void TheHelperFindsAForbiddenCallAndAStaleAllowance()
    {
        var probe = typeof(ForbiddenCallTests).FullName + "." + nameof(Probe);
        var rule = new ForbiddenCallRule(
            Name: "no Math.Max",
            Scope: _ => true,
            Callee: callee => callee.DeclaringType == typeof(Math) && callee.Name == nameof(Math.Max),
            Signature: _ => true,
            AllowList: new HashSet<string>(StringComparer.Ordinal));
        Type[] types = [typeof(ForbiddenCallTests), .. typeof(ForbiddenCallTests).GetNestedTypes(System.Reflection.BindingFlags.NonPublic)];

        var (found, calls) = ForbiddenCalls.Find(types, rule.Callee, rule.Signature, rule.AllowList);
        Assert.True(calls > 0);
        Assert.Contains(found, problem => problem.StartsWith(probe + " calls System.Math.Max(", StringComparison.Ordinal));

        var (allowed, _) = ForbiddenCalls.Find(types, rule.Callee, rule.Signature, new HashSet<string>(StringComparer.Ordinal) { probe });
        Assert.Empty(allowed);

        var (stale, _) = ForbiddenCalls.Find(types, rule.Callee, rule.Signature, new HashSet<string>(StringComparer.Ordinal) { probe, "Nobody.Calls.This" });
        var single = Assert.Single(stale);
        Assert.StartsWith("Nobody.Calls.This is allowed the call", single, StringComparison.Ordinal);
    }

    /// <summary>A call to the forbidden API, once directly and once inside a lambda.</summary>
    private static int Probe(int left, int right)
    {
        int inside(int value) => Math.Max(value, right);
        return Math.Max(left, inside(left));
    }
}
