namespace ProtocolChecks;

/// <summary>
/// One name a ✅ C# block of an <c>API.md</c> declares (<see cref="ApiDeclarations.Declarations"/>): a type, or a member
/// of the type that owns it at that point of the block.
/// </summary>
/// <param name="Name">The declared name.</param>
/// <param name="IsType">A type declaration (class, struct, record, enum, interface, delegate).</param>
/// <param name="IsEnumMember">Read as an enum member: checked only when the owning type is an enum.</param>
internal readonly record struct Declaration(string Name, bool IsType, bool IsEnumMember)
{
    /// <summary>A type declaration that opens a body. The members after a bodiless one (a record closed by a semicolon,
    /// an enum whose whole body sits on its own line) belong to the enclosing type, not to it.</summary>
    public bool OpensBody { get; init; } = true;

    /// <summary>A positional parameter of a record, read from the type's own line: it belongs to that record even when
    /// the record has no body.</summary>
    public bool FromTypeLine { get; init; }
}
