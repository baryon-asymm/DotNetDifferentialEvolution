using System.Reflection;

namespace DotNetDifferentialEvolution.Protocol.Tests;

/// <summary>
/// The frame of every "this call is forbidden here" fact over compiled code: the walk over the IL of every method body
/// (<see cref="IlBody"/>), the call predicate and the signature predicate a tree supplies, an explicit allow-list of
/// callers, the count that lets a fact refuse an empty walk, and the message format. The predicates are the tree's; no
/// predicate of any particular tree lives here. A call from a lambda, local function, iterator or async method is
/// attributed to the method that wrote it, so an allow-list entry names what the author sees.
/// </summary>
internal static class ForbiddenCalls
{
    /// <summary>Every forbidden call in the types of the given assemblies; see <see cref="Find(IEnumerable{Type}, Func{MethodBase, bool}, Func{MethodBase, bool}, IReadOnlySet{string})"/>.</summary>
    public static (IReadOnlyList<string> Problems, int CallsWalked) Find(
        IEnumerable<Assembly> assemblies, Func<MethodBase, bool> calleePredicate, Func<MethodBase, bool> signaturePredicate, IReadOnlySet<string> allowList)
    {
        ArgumentNullException.ThrowIfNull(assemblies);
        return Find(assemblies.Distinct().SelectMany(assembly => assembly.GetTypes()), calleePredicate, signaturePredicate, allowList);
    }

    /// <summary>
    /// Every call, in a method body of the given types, to a method <paramref name="calleePredicate"/> accepts in a form
    /// <paramref name="signaturePredicate"/> accepts, made by a caller not on <paramref name="allowList"/>
    /// (<c>Namespace.Type.Method</c>); plus every allow-list entry no such call needed, which is stale. Also the number of
    /// call instructions walked in total, for the caller to refuse an empty walk.
    /// </summary>
    public static (IReadOnlyList<string> Problems, int CallsWalked) Find(
        IEnumerable<Type> types, Func<MethodBase, bool> calleePredicate, Func<MethodBase, bool> signaturePredicate, IReadOnlySet<string> allowList)
    {
        ArgumentNullException.ThrowIfNull(types);
        ArgumentNullException.ThrowIfNull(calleePredicate);
        ArgumentNullException.ThrowIfNull(signaturePredicate);
        ArgumentNullException.ThrowIfNull(allowList);
        var problems = new SortedSet<string>(StringComparer.Ordinal);
        var allowanceUsed = new HashSet<string>(StringComparer.Ordinal);
        var calls = 0;
        foreach (var type in types)
        {
            foreach (var method in TypeShape.MethodsOf(type))
            {
                foreach (var instruction in IlBody.Instructions(method))
                {
                    if (instruction.Operand is not MethodBase callee)
                    {
                        continue;
                    }

                    calls++;
                    if (!calleePredicate(callee) || !signaturePredicate(callee))
                    {
                        continue;
                    }

                    var caller = CallerName(method);
                    if (allowList.Contains(caller))
                    {
                        _ = allowanceUsed.Add(caller);
                        continue;
                    }

                    _ = problems.Add($"{caller} calls {Describe(callee)}");
                }
            }
        }

        foreach (var stale in allowList.Except(allowanceUsed).Order(StringComparer.Ordinal))
        {
            _ = problems.Add($"{stale} is allowed the call, and no longer makes it: remove the allow-list entry");
        }

        return ([.. problems], calls);
    }

    /// <summary><c>Namespace.Type.Method</c> of the method an author wrote: a compiler-generated closure, iterator or
    /// state machine is attributed to the outermost type and the method named inside its mangled name.</summary>
    public static string CallerName(MethodBase method)
    {
        ArgumentNullException.ThrowIfNull(method);
        var declaring = method.DeclaringType!;
        var name = Unmangle(method.Name) ?? method.Name;
        for (var type = declaring; type is not null && type.Name.StartsWith('<'); type = type.DeclaringType)
        {
            if (Unmangle(type.Name) is { Length: > 0 } written && !method.Name.StartsWith('<'))
            {
                name = written;
            }
        }

        return $"{TypeShape.Outermost(declaring).FullName}.{name}";
    }

    private static string? Unmangle(string name)
    {
        if (!name.StartsWith('<'))
        {
            return null;
        }

        var close = name.IndexOf('>', StringComparison.Ordinal);
        return close > 1 ? name[1..close] : null;
    }

    private static string Describe(MethodBase callee) =>
        $"{callee.DeclaringType?.FullName}.{callee.Name}({string.Join(", ", callee.GetParameters().Select(parameter => parameter.ParameterType.Name))})";
}
