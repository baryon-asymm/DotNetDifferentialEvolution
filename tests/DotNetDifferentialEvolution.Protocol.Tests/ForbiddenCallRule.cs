using System.Reflection;

namespace ProtocolChecks;

/// <summary>
/// One rule against a call, configured in <see cref="ProtocolConfig.ForbiddenCallRules"/> and applied by
/// <see cref="ForbiddenCalls"/>: within the nodes <paramref name="Scope"/> accepts, no method body may call a method
/// <paramref name="Callee"/> accepts in a form <paramref name="Signature"/> accepts, except the callers named in
/// <paramref name="AllowList"/> by <c>Namespace.Type.Method</c>. An allow-list entry no caller needs any more is
/// itself a failure: an exemption that stopped being needed turns red on its own.
/// </summary>
/// <param name="Name">What the rule forbids, in words: the message names it.</param>
/// <param name="Scope">The nodes whose own types are walked.</param>
/// <param name="Callee">Which called API the rule is about (an assembly plus a name prefix, typically).</param>
/// <param name="Signature">Which forms of that API are forbidden (all of them: <c>_ =&gt; true</c>).</param>
/// <param name="AllowList">Callers allowed the forbidden form, by <c>Namespace.Type.Method</c>.</param>
internal sealed record ForbiddenCallRule(
    string Name,
    Func<Node, bool> Scope,
    Func<MethodBase, bool> Callee,
    Func<MethodBase, bool> Signature,
    IReadOnlySet<string> AllowList);
