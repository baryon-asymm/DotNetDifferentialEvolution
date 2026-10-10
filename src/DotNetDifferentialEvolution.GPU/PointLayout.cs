using System.Reflection;
using System.Runtime.CompilerServices;

namespace DotNetDifferentialEvolution.GPU;

/// <summary>
/// The rule for the result type of a pointwise objective (ACCEPTANCE.md, A5). The kernels write the point results to a
/// device buffer whose stride is the natural size of the type, the C layout with every field at a multiple of its own
/// alignment; the host reads the buffer back with the runtime's size. A type whose two sizes differ would have the
/// kernel write out of bounds, so only the types for which they agree are accepted: sequential layout, fields of
/// <see cref="byte"/>, <see cref="sbyte"/>, <see cref="short"/>, <see cref="ushort"/>, <see cref="int"/>,
/// <see cref="uint"/>, <see cref="long"/>, <see cref="ulong"/>, <see cref="float"/> or <see cref="double"/>, enums of
/// them, or structs that pass the same rule, and a runtime size equal to the natural one.
/// </summary>
internal static class PointLayout
{
    private const string Allowed =
        "byte, sbyte, short, ushort, int, uint, long, ulong, float, double, enums of them, and structs of sequential layout whose fields follow the same rule";

    private static readonly Dictionary<Type, int> PrimitiveSizes = new()
    {
        [typeof(byte)] = 1,
        [typeof(sbyte)] = 1,
        [typeof(short)] = 2,
        [typeof(ushort)] = 2,
        [typeof(int)] = 4,
        [typeof(uint)] = 4,
        [typeof(float)] = 4,
        [typeof(long)] = 8,
        [typeof(ulong)] = 8,
        [typeof(double)] = 8,
    };

    private static readonly MethodInfo SizeOfDefinition = typeof(Unsafe).GetMethod(nameof(Unsafe.SizeOf))!;

    /// <summary>Refuses a point type the kernels cannot lay out.</summary>
    /// <typeparam name="TPoint">The type of one point's result.</typeparam>
    /// <exception cref="ArgumentException">The type breaks the rule; the message names the type and the field.</exception>
    internal static void Require<TPoint>()
        where TPoint : unmanaged =>
        _ = Measure(typeof(TPoint), typeof(TPoint), string.Empty, nameof(TPoint));

    private static (int Size, int Alignment) Measure(Type root, Type type, string path, string parameterName)
    {
        var underlying = type.IsEnum ? Enum.GetUnderlyingType(type) : type;
        if (SizeOfPrimitive(underlying) is { } primitive)
        {
            return (primitive, primitive);
        }

        if (!type.IsValueType || type.IsPrimitive)
        {
            throw Refusal(root, path, type, parameterName, "is of a type the kernels cannot lay out");
        }

        if (!type.IsLayoutSequential)
        {
            var kind = type.IsExplicitLayout ? "Explicit" : "Auto";
            throw Refusal(root, path, type, parameterName, $"has {kind} layout, whose field offsets the kernels cannot know; it must be sequential");
        }

        var fields = type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (fields.Length == 0)
        {
            throw Refusal(root, path, type, parameterName, "has no fields");
        }

        var parts = new List<(FieldInfo Field, int Size, int Alignment)>(fields.Length);
        var offset = 0;
        var alignment = 1;
        foreach (var field in fields)
        {
            var (size, fieldAlignment) = Measure(root, field.FieldType, Join(path, field), parameterName);
            parts.Add((field, size, fieldAlignment));
            offset = AlignUp(offset, fieldAlignment) + size;
            alignment = Math.Max(alignment, fieldAlignment);
        }

        var natural = AlignUp(offset, alignment);
        var actual = (int)SizeOfDefinition.MakeGenericMethod(type).Invoke(null, null)!;
        if (actual != natural)
        {
            var sizes = $"{Display(type)} occupies {actual} bytes where the natural layout of its fields takes {natural}";
            const string consequence = "and the kernels stride the results by the natural size";
            throw OffendingField(type, parts) is { } offender
                ? Refusal(
                    root,
                    Join(path, offender),
                    offender.FieldType,
                    parameterName,
                    $"is not at its natural offset ({sizes}, packed by StructLayout), {consequence}")
                : Refusal(root, path, type, parameterName, $"is padded by StructLayout: it occupies {actual} bytes where the natural layout of its fields takes {natural}, {consequence}");
        }

        return (natural, alignment);
    }

    /// <summary>
    /// The first field of <paramref name="type"/> that the runtime places elsewhere than the natural layout does, under
    /// the type's <c>Pack</c>; <see langword="null"/> when none does (the size alone differs).
    /// </summary>
    private static FieldInfo? OffendingField(Type type, List<(FieldInfo Field, int Size, int Alignment)> parts)
    {
        var pack = type.StructLayoutAttribute?.Pack ?? 0;
        var natural = 0;
        var packed = 0;
        foreach (var (field, size, alignment) in parts)
        {
            natural = AlignUp(natural, alignment);
            packed = AlignUp(packed, pack == 0 ? alignment : Math.Min(alignment, pack));
            if (natural != packed)
            {
                return field;
            }

            natural += size;
            packed += size;
        }

        return null;
    }

    private static ArgumentException Refusal(Type root, string path, Type type, string parameterName, string reason)
    {
        var subject = path.Length == 0 ? "it" : $"its field '{path}' ({Display(type)})";
        return new ArgumentException(
            $"The point type {Display(root)} is not supported: {subject} {reason}. A point type must be {Allowed}.",
            parameterName);
    }

    private static string Join(string path, FieldInfo field)
    {
        const string suffix = ">k__BackingField";
        var name = field.Name.StartsWith('<') && field.Name.EndsWith(suffix, StringComparison.Ordinal)
            ? field.Name[1..^suffix.Length]
            : field.Name;
        return path.Length == 0 ? name : $"{path}.{name}";
    }

    private static string Display(Type type) => type.FullName ?? type.Name;

    private static int AlignUp(int offset, int alignment) => (offset + alignment - 1) / alignment * alignment;

    private static int? SizeOfPrimitive(Type type) => PrimitiveSizes.TryGetValue(type, out var size) ? size : null;
}
